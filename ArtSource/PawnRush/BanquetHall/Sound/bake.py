# R844 (user 11-26: "대연회장 사운드 작업 읽고 시작해", after the R843 design Docs/PawnRush/PAWN_RUSH_BANQUET_HALL_SFX.md):
# the grand banquet hall's sounds. The takes are ElevenLabs sound effects (flow "R844 대연회장 소리" qw0YQDFYqX3Q4pU1s5TP,
# eleven_text_to_sound_v2, 4 takes a prompt; prompts.md) in raw/<kind>_<n>.mp3. This script measures every take and picks
# one per kind by the numbers (the AI cannot listen), or the one picks.json names, and writes
# Assets/Resources/BanquetHallSfx/<Name>.wav (GrandBanquetHallSound.cs plays them) with a .meta beside each new clip
# (mono, PCM, preloaded, no import normalise - the observatory's settings). Every take, trimmed the same way, goes to
# takes/ for the listening page (page.py). The design wants the six cue sounds picked by ear (section 6, item 5).
#   uv run --no-project --python 3.12 --with miniaudio --with numpy python -I bake.py <this folder> <project root>
import os, sys, json, wave, hashlib, numpy as np, miniaudio

S, ROOT = sys.argv[1], sys.argv[2]
sys.path.insert(0, os.path.join(ROOT, 'ArtSource/Loading/SwordPick/Sound'))
from coin2 import rings

SR = 44100
OUT_REL = 'Assets/Resources/BanquetHallSfx'
OUT = os.path.join(ROOT, OUT_REL)
TAKES = os.path.join(S, 'takes')

# kind: (file name, loop, longest it may ring on after its loudest point (s; loops: None), starts with a build-up,
#        a ringing tone is what it is meant to have)
KINDS = {
    # 1st: the cue sounds (design 2-1)
    'drum_roll':        ('BhDrumRoll',        False, .2,  True,  False),
    # the trick's cue as magic instead of the drum (user 11-26 "드럼 사운드 보다는 마법 같은 소리가 더 잘어울리겠어")
    'magic_build':      ('BhMagicBuild',      False, .2,  True,  True),
    'cloth_pull':       ('BhClothPull',       False, .55, False, True),
    'service_bell':     ('BhServiceBell',     False, .5,  False, True),
    'cloche_shut':      ('BhClocheShut',      False, .6,  False, True),
    'lamp_chime':       ('BhLampChime',       False, .6,  False, True),
    'curtain_cue':      ('BhCurtainCue',      False, .4,  False, False),
    # 2nd: the devices (design 2-2)
    'hall_ambience':    ('BhHallAmbience',    True,  None, False, True),
    'kitchen_door':     ('BhKitchenDoor',     False, .35, False, False),
    'tray_clink':       ('BhTrayClink',       True,  None, False, True),
    'cloth_thrown':     ('BhClothThrown',     False, .3,  True,  False),
    'cloth_lay':        ('BhClothLay',        False, .4,  False, False),
    'cloche_open':      ('BhClocheOpen',      False, .4,  False, True),
    'hoist_loop':       ('BhHoistLoop',       True,  None, False, False),
    'gown_loop':        ('BhGownLoop',        True,  None, False, True),
    'curtain_draw':     ('BhCurtainDraw',     True,  None, False, False),
    'chocolate_loop':   ('BhChocolateLoop',   True,  None, False, False),
    'chandelier_swing': ('BhChandelierSwing', False, .7,  False, True),
    'arrival':          ('BhArrival',         False, .6,  False, True),
    # 3rd: the finishing touches (design 2-3; R845 이어서, user 11-26 "나머지 사운드 작업 시작")
    'curtain_meet':     ('BhCurtainMeet',     False, .35, False, False),
    'great_chandelier': ('BhGreatChandelier', True,  None, False, True),
    'guests_toast':     ('BhGuestsToast',     False, .9,  False, True),
    'bass_bump':        ('BhBassBump',        False, .6,  False, True),
    # the chocolate's footsteps: the three best takes go to the shared step folder (PawnRushStepSfx), not this table
    'choco_step':       ('ChocoStep',         False, .2,  False, False),
}
# kinds written elsewhere (the footsteps: STEP_OUT/<name><i>.wav, the best STEP_COUNT takes)
STEPS = {'choco_step'}
STEP_OUT_REL, STEP_COUNT = 'Assets/Resources/PawnRushStepSfx', 3
# Every one-shot is peak-normalised to this and every loop's RMS to LoopRms (its peak no higher than OneShotPeak);
# how loud each is heard is the code's (GrandBanquetHallSound's volumes).
OneShotPeak, LoopRms = -1.0, -20.0
# One-shots kept whole up to this many seconds (a roll or a lay that lasts its device's phase, three taps, a fanfare):
# only their edges are faded, and the numbers only look for a faint take and a late start.
WHOLE = {'drum_roll': 2.0, 'magic_build': 2.0, 'curtain_cue': 1.1, 'cloth_lay': 2.0, 'arrival': 1.6}
HEAVY = set()

# How loud each is heard, close to it, at the settings' default effects volume (PAWN_RUSH_AUDIO.md section 0; the
# design's 2-1 / 2-2 tables); bake.py prints the AudioSource volume that gives it (`vol`), which the code's table carries.
EFFECTS = .8
HEARD = dict(drum_roll=-20, magic_build=-20, cloth_pull=-18, service_bell=-21, cloche_shut=-21, lamp_chime=-22, curtain_cue=-21,
             hall_ambience=-34, kitchen_door=-24, tray_clink=-30, cloth_thrown=-21, cloth_lay=-24, cloche_open=-24,
             hoist_loop=-27, gown_loop=-32, curtain_draw=-27, chocolate_loop=-30, chandelier_swing=-26, arrival=-21,
             curtain_meet=-24, great_chandelier=-33, guests_toast=-26, bass_bump=-24)


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
        # a drum roll should build to its end, where the pull follows, and last its whole two seconds
        if kind in ('drum_roll', 'magic_build'): s += .5 * max(0.0, WHOLE[kind] - m['hit']) + 5 * max(0.0, WHOLE[kind] - m['length'])
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
        y = x[start:start + int(WHOLE[kind] * SR)].copy()
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


def meta(name, out_rel=OUT_REL):
    rel = f'{out_rel}/{name}.wav'
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
    if kind in STEPS:
        # the best few, each its own step clip (peak -1 dB; the step code sets their level)
        best = sorted(rows, key=lambda r: r[0]['score'])[:STEP_COUNT]
        for i, (m, x) in enumerate(best):
            write_wav(os.path.join(ROOT, STEP_OUT_REL, f'{name}{i}.wav'), trim(x, m, kind))
            print(f"{kind:14s} take {m['take']} -> {STEP_OUT_REL}/{name}{i}.wav  meta {meta(f'{name}{i}', STEP_OUT_REL)}")
        report[kind] = dict(file=name + '0..', picked=[f"{kind}_{r[0]['take']}" for r in best], by='numbers', loop=False,
                            takes=[{k: (round(v, 3) if isinstance(v, float) else v) for k, v in r[0].items()} for r in rows])
        continue
    best = min(rows, key=lambda r: r[0]['score'])
    want = str(chosen.get(kind, best[0]['take']))
    if '+' in want:
        # takes mixed (user 11-26 "빼기 1번 2번 혼합"): each trimmed as it would be alone, laid so their loudest moments
        # meet, summed and normalised again
        parts = []
        for w in want.split('+'):
            src = w if '_' in w else f'{kind}_{w}'
            mm, xx = measured[src]
            parts.append(trim(xx, measure(xx, kind), kind))
        hits = [int(np.argmax(env(p))) for p in parts]
        lead = max(hits)
        length = max(lead - h + len(p) for p, h in zip(parts, hits))
        y = np.zeros(length)
        for p, h in zip(parts, hits):
            y[lead - h:lead - h + len(p)] += p
        y *= 10 ** (OneShotPeak / 20) / max(np.abs(y).max(), 1e-9)
        source = kind + '_' + want
    else:
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
