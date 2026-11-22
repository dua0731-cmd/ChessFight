"""R675: bakes the palace mini-games' sounds from the user's ElevenLabs takes in raw/MiniGame/ (prompts in
Docs/PawnRush/PAWN_RUSH_AUDIO.md section 7, written 2026-11-15) into Assets/Resources/PawnRushMiniGameSfx/<same name>.wav,
where ArenaFx plays them by name (ArenaFx.Sfx for one-shots, ArenaFx.Hold(loop: true) for the loops).

One-shots: mono (the mid of the two channels, or the louder channel where the two are not alike: a sum of unlike
channels thins the sound), cut to the moment (ONE_SHOTS: from `start`, or from the hit found after `hit_from` -
2 ms before the first 2 ms within 12 dB of the loudest - to `end` in the take), 1 ms fade in, `fade` out, an
optional high-pass (`hp`, the rumble under the chimes), then levelled: the loudest 400 ms (the whole clip when it is
shorter, as R654 measured every clip) to `level`, peaks held under -1 dBFS by a look-ahead limiter.
Loops (LOOPS): the stretch between `shortest` and `longest` seconds whose start and end sound most alike (spectrum
over 0.4 s, loudness over 3 s), joined end to start with a 0.4 s equal-power crossfade, RMS to `level`.

Levels follow the R654 yardstick (Docs/PawnRush/PAWN_RUSH_AUDIO.md section 0): what is heard at default settings
(effects 0.8) and close by, with the volume each call passes: level = heard - 20 log10(volume x 0.8).

Writes a .meta beside each new clip (deterministic GUID as Tools/Generators/mkmeta.py, mono, PCM in memory,
preloaded, no import normalise so the levels here hold); an existing .meta is kept.

Needs only numpy: run with Blender's Python from the repository root:
  "C:/Program Files/Blender Foundation/Blender 5.1/5.1/python/bin/python.exe" -I ArtSource/PawnRush/Sfx/bake_minigame.py
"""
import hashlib, os, wave
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '../../..'))
RAW = os.path.join(HERE, 'raw', 'MiniGame')
OUT_REL = 'Assets/Resources/PawnRushMiniGameSfx'
OUT = os.path.join(ROOT, OUT_REL)
EFFECTS = .8   # GameSettings.Effects default

# name: where the moment is in the take (s), how it ends, the call's volume and how loud it should be heard (dB)
ONE_SHOTS = {
    # every mini-game: the team's game done (ArenaDressing, 2D, 1 ours / .35 theirs; PromotionChooser .6)
    'sfx_mg_complete': dict(hit_from=0, end=1.0, fade=.15, vol=1., heard=-18.5),
    # the promotion pads open (PromotionChooser, 2D, pitch 1.5): its rise is the sound, kept from 0
    'sfx_mg_quarter': dict(start=0, end=.45, fade=.04, hp=250, vol=1., heard=-24),
    # F ballroom (MgEnPassant): the wind-up rattle before a statue moves (.6), its two steps .15 s apart, the
    # 1.2 s window opening (.8), a pawn bumping too early, a capture (also I's knight and J's pads, pitched up there)
    'sfx_ep_ready': dict(hit_from=0, end=.45, fade=.05, vol=.6, heard=-25),
    'sfx_ep_step': dict(start=.31, end=.45, fade=.04, vol=1., heard=-25),     # 36 ms of slide, then the clack
    'sfx_ep_window': dict(start=0, end=.486, fade=.12, hp=300, vol=.8, heard=-21),
    'sfx_ep_clank': dict(hit_from=.9, end=1.18, fade=.04, vol=1., heard=-21),  # the take's loudest three ratchet clicks
    # the capture's clack and the castling thud open on a spike 20 dB over their body: limited harder (`most`)
    'sfx_ep_capture': dict(hit_from=0, end=.45, fade=.05, vol=1., heard=-20, most=12.),
    # R770 (user 11-22, "무도회장 부터 시작"; ElevenLabs flow "F 대무도회장 빈 소리" xHQF1q4D6Ks0S9PfZ3jX, takes in
    # raw/MiniGame/takes_R771/): a taken doll landing on the tray bench (take 1: one wooden clack and a short gear
    # rattle, gone by 0.35 s; also H's tray at .34) and F's mirror door rising 4.6 m in 1.2 s (take 3: a 1 s rising
    # glide, the one whose body lasts as long as the door moves; MiniGameDoorGateFx.GateSound)
    'sfx_ep_tray': dict(hit_from=0, end=.45, fade=.08, vol=1., heard=-22, most=12.),
    'sfx_ballroom_gate': dict(hit_from=0, end=1.6, fade=.4, vol=1., heard=-21),
    # R772 (user 11-22, "G랑 H 시작"; flow "G · H 미니게임 빈 소리" 4D8axxzwyD2J4mVnWgVN, takes in raw/MiniGame/takes_R772/):
    # G's and H's doors, composed in their moments' time by compose_gates.py, so cut from 0 (a found hit would shift
    # them off the picture)
    'sfx_procession_gate': dict(start=0, end=1.8, fade=.5, vol=1., heard=-21),
    'sfx_chesshall_gate': dict(start=0, end=1.8, fade=.5, vol=1., heard=-21),
    # H checkmate (MgCheckmate, the volume each call passes): a check (.48, pitch rising with the checks; take 3, one
    # tick), a king frozen (1 since R776; R776: takes_R776 v2_3, a wooden click and a short brass ting at 2.45 kHz - R772's take 4, two low mallet notes at 216 / 435 Hz, sounded like a "뿡" to the user), a capture (1; take 2, the least hiss),
    # the last capture (.82; take 2, a rising E-G-B-E arpeggio), a rescue (1; take 2, two clicks 0.28 s apart),
    # a stalemate jump (1; take 1, the one airy flick), a hop (.5; take 1, the only tap not 12 dB down)
    'sfx_check': dict(hit_from=0, end=.25, fade=.05, vol=.48, heard=-24, most=12.),
    'sfx_mate': dict(hit_from=0, end=.35, fade=.1, vol=1., heard=-22, most=12.),   # R776: take v2_3 (see above)
    'sfx_king_capture': dict(hit_from=0, end=.45, fade=.1, vol=1., heard=-21, most=12.),
    'sfx_king_sweep': dict(hit_from=0, end=.9, fade=.3, vol=.82, heard=-19),
    'sfx_rescue': dict(hit_from=0, end=.48, fade=.06, vol=1., heard=-24, most=12.),
    'sfx_stalemate': dict(hit_from=0, end=.45, fade=.1, vol=1., heard=-24),
    'sfx_king_hop': dict(hit_from=0, end=.15, fade=.04, vol=.5, heard=-26, most=12.),
    # R773 (user 11-22, "여기 움직일 때 나는 소리도 해"): one of our pawns steps onto a new square and its attacked-square
    # plates light (MgCheckmate.PlateStep, .5). Takes in raw/MiniGame/takes_R773/: the first prompt (light switching on)
    # gave four rumbles or near-silence; the second (fingertips over tiny glass chimes) take 3, the only one that swells
    # in and is gone by 0.45 s. Heard about as a footstep, since it plays at every step
    'sfx_attack_squares': dict(start=0, end=.42, fade=.15, vol=.5, heard=-28),
    # R774 (user 11-22, "확인 다음" -> I; flow "I 국왕 침실 빈 소리" 3PGmVTsMBVB9HcMuDFOw, takes in raw/MiniGame/takes_R774/):
    # I check block (MgCheckBlock). A shield cart locked on its square (take 3: a short slide at 0.03, the clunk at
    # 0.24, the least hiss), a knight toppled (take 1: wobble then wooden clatter, the deepest), the double check rising
    # (take 2: two bell strikes 0.26 s apart at 1027 / 987 Hz, beating), the double check solved (take 1: D6 then F6,
    # the only two-note one; all four came 40 dB down, so this one is raised the most)
    'sfx_shield_lock': dict(hit_from=0, end=.6, fade=.12, vol=1., heard=-21, most=12.),
    'sfx_piece_fall': dict(hit_from=0, end=.7, fade=.15, vol=1., heard=-21, most=12.),
    'sfx_double_check': dict(hit_from=0, end=.75, fade=.15, vol=1., heard=-20),
    'sfx_resolve': dict(hit_from=0, end=.6, fade=.12, vol=1., heard=-22),
    # the shared team door (MiniGameDoorGateFx for I, ArenaDressing's fallback): G's and H's own wooden door rising,
    # gate_wood_rise take 1 (R772) on its own - I's door is the same G_TeamGateDoor
    'sfx_gate_open': dict(start=0, end=1.7, fade=.4, vol=1., heard=-21),
    # R777 (user 11-22, "확인 j 세탁장"; flow "J 왕궁 세탁장 빈 소리" nIjEy6DLNDaqpXbkTc6R, takes in raw/MiniGame/takes_R777/):
    # J laundry. The sluice gate hauled up and the soapy gush under it (MiniGameLaundryFx.Opened, 1; take 4: swells from
    # quiet to its loudest at 0.5 - 0.9 s and calms over the rest, as the 2.5 s gush does) and a fall into the soapy water
    # (MgKnightPads .6 / MgWaterReturn 1; take 1: the least hiss of four bright splashes; was a garden splash). The pad's
    # jet (sfx_pad_launch) was made too (takes_R777 sfx_pad_launch_*) and taken out again: the user liked the garden's
    # droplet splash it stands in for better ("세탁장 전에 있던 점프 했을 때 물방울 소리가 더 나아"), so PadSplash plays
    'sfx_laundry_gate': dict(start=0, end=2.9, fade=.8, vol=1., heard=-21),
    'sfx_splash': dict(hit_from=0, end=1.05, fade=.25, vol=1., heard=-20),
    # R778 (user 11-22, "낙하 · 복귀 시작"; flow "공용 낙하 · 복귀 소리" DRKko7Y2IUGNbouzEKmz, takes in raw/MiniGame/takes_R778/):
    # every course section's own-pawn fall and return (PawnRushFallNotice, 2D, the stand-ins SkillFail / sfx_mg_quarter
    # until now). The fall's prompt drops section 9's closing thud, since the same sound plays for a fall into water.
    # Fall: take 4, a slide whistle gliding 1.8 -> 0.83 kHz in 0.5 s (the quickest of four clean glides); return: take 4,
    # a chime stepping up 1.5 -> 1.7 -> 2.0 kHz (the only two-step rise; it came 34 dB down and is raised the most)
    'sfx_fall': dict(hit_from=0, end=.62, fade=.15, vol=.8, heard=-21),
    'sfx_respawn': dict(hit_from=0, end=.45, fade=.12, vol=.7, heard=-22),
    # G procession (MgKingEscort): the sled stopped by a rook, a rook latched in its garage, the sled home (castling)
    'sfx_sled_blocked': dict(hit_from=.4, end=.95, fade=.1, vol=1., heard=-21),
    'sfx_latch': dict(hit_from=0, end=.25, fade=.03, vol=1., heard=-22),
    'sfx_castle_land': dict(hit_from=0, end=.8, fade=.12, vol=1., heard=-19, most=12.),
}
# the escort's loops, each held at .6 while the sled is pulled (its pitch .8 - 1.2 with the speed), on sand, a rook moving
LOOPS = {
    'sfx_sled_drag': dict(shortest=6., longest=10., vol=.6, heard=-30),
    'sfx_sand_crunch': dict(shortest=6., longest=10., vol=.6, heard=-31),   # on top of the drag
    'sfx_stone_drag': dict(shortest=6., longest=10., vol=.6, heard=-30),
    # R774, I: a shield cart pushed along its rail (.6; take 3, the fewest knocks in it) and a check line live (.35 each,
    # up to seven at once, so lower; take 2, the only one with a hum above 250 Hz - the others are sub-bass a laptop
    # cannot play). 6 s ElevenLabs loop-mode takes, kept whole (too short for loop()'s 1.5 s of context either side)
    'sfx_cart_roll': dict(whole=True, vol=.6, heard=-30),
    'sfx_beam_hum': dict(whole=True, vol=.35, heard=-33),
}
CEILING = 10 ** (-1 / 20)
MOST_LIMIT = 8.   # dB the peak limiter may take off a clip


def load(name):
    w = wave.open(os.path.join(RAW, name + '.wav'))
    x = np.frombuffer(w.readframes(w.getnframes()), np.int16).astype(np.float64).reshape(-1, w.getnchannels()) / 32768
    return x, w.getframerate()


def mono(x):
    if x.shape[1] == 1: return x[:, 0], 'mono'
    corr = np.corrcoef(x[:, 0], x[:, 1])[0, 1]
    if corr >= .2: return x.mean(1), f'mid (L/R likeness {corr:.2f})'
    louder = int((x[:, 1] ** 2).sum() > (x[:, 0] ** 2).sum())
    return x[:, louder], f'{"LR"[louder]} channel (L/R likeness {corr:.2f})'


def hit(m, sr, frm, end):
    """2 ms before the first 2 ms (after `frm`) that comes within 12 dB of the loudest 2 ms before `end`."""
    k = int(.002 * sr)
    a, b = int(frm * sr), int(end * sr)
    e = np.array([np.mean(m[i:i + k] ** 2) for i in range(a, b - k, k)])
    db = 10 * np.log10(e + 1e-12)
    i = int(np.argmax(db >= db.max() - 12))
    return max(0, a + (i - 1) * k) / sr


def high_pass(y, sr, hz):
    """Two passes of a 2nd-order Butterworth high-pass (RBJ cookbook), forward only: 24 dB/octave, no pre-echo."""
    w0 = 2 * np.pi * hz / sr
    al = np.sin(w0) / (2 * np.sqrt(.5))
    c = np.cos(w0)
    b0, b1, b2 = (1 + c) / 2, -(1 + c), (1 + c) / 2
    a0, a1, a2 = 1 + al, -2 * c, 1 - al
    b0, b1, b2, a1, a2 = b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0
    for _ in range(2):
        out = np.zeros_like(y)
        x1 = x2 = y1 = y2 = 0.
        for n, xn in enumerate(y):
            yn = b0 * xn + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
            out[n] = yn
            x2, x1, y2, y1 = x1, xn, y1, yn
        y = out
    return y


def limit(y, sr):
    """Peaks under CEILING: the gain each sample needs, held 1 ms either side (look-ahead), recovering over 10 ms,
    smoothed over 1 ms. Returns the signal and the most it took off, dB."""
    r = max(1, int(.001 * sr))
    need = np.minimum(1., CEILING / np.maximum(np.abs(y), 1e-9))
    if need.min() >= 1.: return y, 0.
    pad = np.pad(need, (r, r), constant_values=1.)
    held = np.lib.stride_tricks.sliding_window_view(pad, 2 * r + 1).min(1)
    rel = 1 - np.exp(-1 / (.01 * sr))
    g = np.empty_like(held)
    cur = 1.
    for i, h in enumerate(held):
        cur = min(h, cur + (1 - cur) * rel)
        g[i] = cur
    k = r // 2 * 2 + 1
    g = np.convolve(np.pad(g, (k // 2, k // 2), mode='edge'), np.ones(k) / k, 'valid')
    return y * g, -20 * np.log10(g.min())


def loudness(y, sr):
    """R654's short loudness: the loudest 400 ms, or the whole clip when it is shorter (dBFS)."""
    k = int(.4 * sr)
    if len(y) <= k: return 10 * np.log10(np.mean(y ** 2) + 1e-12)
    return 10 * np.log10(np.convolve(y ** 2, np.ones(k) / k, 'valid').max() + 1e-12)


def rms(y): return 10 * np.log10(np.mean(y ** 2) + 1e-12)


def loud100(y, sr):
    k = int(.1 * sr)
    if len(y) <= k: return rms(y)
    return 10 * np.log10(np.convolve(y ** 2, np.ones(k) / k, 'valid').max() + 1e-12)


def level(y, sr, want, measure, most=MOST_LIMIT):
    """Gain to `want` by `measure`, the limiter on the peaks, measured again and corrected (three rounds). The
    limiter takes at most `most` dB off: a clack whose 400 ms is mostly silence (the capture, the latch) would be
    crushed flat to reach the 400 ms level, so it stays under it with its hit whole (its 100 ms is in the report)."""
    g = 10 ** ((want - measure(y, sr)) / 20)
    for _ in range(3):
        out, took = limit(y * g, sr)
        g *= 10 ** ((want - measure(out, sr)) / 20)
    out, took = limit(y * g, sr)
    if took <= most: return out, took
    lo, hi = 0., g
    for _ in range(20):
        mid = (lo + hi) / 2
        if limit(y * mid, sr)[1] > most: hi = mid
        else: lo = mid
    return limit(y * lo, sr)


def bands(seg, sr):
    s = np.abs(np.fft.rfft(seg * np.hanning(len(seg)))) ** 2
    f = np.fft.rfftfreq(len(seg), 1 / sr)
    edges = np.geomspace(60, 16000, 25)
    return np.array([10 * np.log10(s[(f >= lo) & (f < hi)].mean() + 1e-14) for lo, hi in zip(edges[:-1], edges[1:])])


def loop(m, sr, shortest, longest, xf=.4):
    """The stretch whose start and end sound most alike, its end crossfaded into its start. Returns the loop and
    (start s, length s, spectrum likeness dB, loudness likeness dB)."""
    hop, win = int(.05 * sr), int(.1 * sr)
    starts = range(0, len(m) - win, hop)
    S = np.array([bands(m[i:i + win], sr) for i in starts])
    E = np.array([10 * np.log10(np.mean(m[i:i + hop] ** 2) + 1e-12) for i in starts])
    F, X, ctx = len(S), int(round(xf / .05)), 30
    best = None
    for L in range(int(shortest / .05), int(longest / .05) + 1):
        for i in range(ctx, F - L - max(X, ctx)):
            j = i + L
            spec = np.abs(S[i:i + X] - S[j:j + X]).mean()
            env = np.abs(E[i - ctx:i + ctx] - E[j - ctx:j + ctx]).mean()
            score = spec + .5 * env
            if best is None or score < best[0]: best = (score, i, j, spec, env)
    _, i, j, spec, env = best
    a, b = i * hop, j * hop
    n = int(xf * sr)
    # the join on the best-matching sample within 25 ms (helps where the sound has a pitch, harmless in noise)
    ref = m[a:a + n]
    shifts = range(-hop // 2, hop // 2)
    d = max(shifts, key=lambda s: float(np.dot(ref, m[b + s:b + s + n])) / (np.linalg.norm(m[b + s:b + s + n]) + 1e-9))
    b += d
    N = b - a
    seg = m[a:b + n]
    out = seg[:N].copy()
    t = (np.arange(n) + .5) / n
    out[:n] = seg[:n] * np.sin(t * np.pi / 2) + seg[N:N + n] * np.cos(t * np.pi / 2)
    return out, (a / sr, N / sr, spec, env)


def write(path, y, sr):
    with wave.open(path, 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr)
        w.writeframes(np.rint(np.clip(y, -1, 1) * 32767).astype('<i2').tobytes())


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


def meta(name):
    rel = f'{OUT_REL}/{name}.wav'
    path = os.path.join(ROOT, rel) + '.meta'
    if os.path.exists(path): return 'kept'
    g = hashlib.md5(('chessfight:' + rel).encode()).hexdigest()
    with open(path, 'w', newline='\n') as f: f.write(META.format(g=g))
    return 'new'


def gain_for(c): return c['heard'] - 20 * np.log10(c['vol'] * EFFECTS)


os.makedirs(OUT, exist_ok=True)
print('name\tfrom\tcut_s\tlength_s\tfile_dB\theard_400ms/100ms_dB\tlimiter_dB\tpeak_dB\tmeta')
for name, c in ONE_SHOTS.items():
    x, sr = load(name)
    m, how = mono(x)
    start = c['start'] if 'start' in c else hit(m, sr, c['hit_from'], c['end'])
    y = m[int(start * sr):int(c['end'] * sr)].copy()
    if c.get('hp'): y = high_pass(y, sr, c['hp'])
    r = max(1, int(.001 * sr))
    y[:r] *= np.linspace(0, 1, r)
    f = int(c['fade'] * sr)
    y[-f:] *= np.cos(np.linspace(0, np.pi / 2, f)) ** 2
    y, took = level(y, sr, gain_for(c), loudness, c.get('most', MOST_LIMIT))
    write(os.path.join(OUT, name + '.wav'), y, sr)
    to_heard = 20 * np.log10(c['vol'] * EFFECTS)
    print(f'{name}\t{how}\t{start:.3f}-{c["end"]:.3f}\t{len(y) / sr:.3f}\t{loudness(y, sr):.1f} (want {gain_for(c):.1f})'
          f'\t{loudness(y, sr) + to_heard:.1f} / {loud100(y, sr) + to_heard:.1f}'
          f'\t{took:.1f}\t{20 * np.log10(np.abs(y).max()):.1f}\t{meta(name)}')
for name, c in LOOPS.items():
    x, sr = load(name)
    m, how = mono(x)
    if c.get('whole'):
        # R774: an ElevenLabs loop-mode take already runs end into start; kept whole. Its seam is reported as the
        # step from the last sample to the first against the usual step inside it (as R675 measured the escort's).
        y, a, n = m.copy(), 0., len(m) / sr
        spec, env = abs(m[-1] - m[0]), float(np.mean(np.abs(np.diff(m))))
    else:
        y, (a, n, spec, env) = loop(m, sr, c['shortest'], c['longest'])
    y, took = level(y, sr, gain_for(c), lambda v, s: rms(v))
    write(os.path.join(OUT, name + '.wav'), y, sr)
    print(f'{name}\t{how}\t{a:.3f}+{n:.3f} (ends alike: spectrum {spec:.1f} dB, loudness {env:.1f} dB)\t{len(y) / sr:.3f}'
          f'\t{rms(y):.1f} RMS (want {gain_for(c):.1f})\t{took:.1f}\t{20 * np.log10(np.abs(y).max()):.1f}\t{meta(name)}')
