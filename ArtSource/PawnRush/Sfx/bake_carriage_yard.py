"""Royal Carriage Yard sounds (2026-11-17, 승규 "1번 말 마차" -> "너가 만들어 일레븐랩스 mcp 연결해서"): bakes the takes made
with ElevenLabs sound effects (eleven_text_to_sound_v2, prompts in Docs/PawnRush/PAWN_RUSH_AUDIO.md section 9) in
raw/CarriageYard/ into Assets/Resources/PawnRushCarriageYardSfx/, where RoamingCarriage plays them:

  HoofLight0..5  one hoof on the cobbles at a walk or trot     (YardHorse.Footfall, below a half gallop)
  HoofHeavy0..5  one hoof at a gallop                           (YardHorse.Footfall, a half gallop and over)
  HorseSnort0..1, HorseNeigh0..1   a team about to charge       (the start of its aim, lamps red)
  CarriageHit0..1                  a team knocking a pawn away  (YardKit.HitGiven from the team's shell)
  SlamThud0..1, SlamShock0..1      the giant chest set down and its wave  (SlamChest, as it lands)
  ChainLower0..1, ChainHoist0..1   its chains lowering and hoisting it    (SlamChest, as each starts)
  StackSet0..1, StackMove0..1      a storage row's stack set down, lifted and carried  (StackRow)
  ConveyorBelt, ConveyorRide       loops: the conveyor running, a load rattling on it   (CargoConveyor)
  WheelTurn, WaterPour, WaterFlow  loops: the wheels, the falling water, the canal and pond  (YardSoundscape)
  CraneSlew (loop), HoistMove0..1, HoistLand0..1   a timber crane slewing; a loft hoist's ropes and pallets  (SlewCrane, PalletHoist)
  GateOpen0..1, GatePush (loop)    the outer gate opening at the start; the final gate pushed open  (TimedGate, PushGate)
  HitBump0..2, HitPaddle0..2      a pawn knocked away by a crate/stack/pallet/load; by a waterwheel's paddle  (YardSoundscape)

Hooves: every take is a row of single strikes; each strike is cut from 2 ms before its onset to the next one (at
most `longest`), and the six cleanest of all the takes (loud, little left at the cut) are kept, each levelled alone.
One-shots: cut from 2 ms before the first hit to where the sound has died 40 dB under its loudest, then levelled.
Mono (the mid where the two channels are alike), 1 ms fade in, a cosine fade out, peaks under -1 dBFS (look-ahead
limiter, at most MOST dB). Levels follow R654 (PAWN_RUSH_AUDIO section 0): what is heard at default effects (0.8)
close by with the call's volume; short clicks are measured over their whole length.

Writes a .meta beside each new clip (deterministic GUID, mono, PCM in memory, preloaded, no import normalise);
an existing .meta is kept. The takes come as MP3: decode them to WAV first with Blender (aud), e.g.
  blender -b --factory-startup --python <a script that writes aud.Sound(mp3).data() to .wav>
Needs only numpy: from the repository root,
  "C:/Program Files/Blender Foundation/Blender 5.2/5.2/python/bin/python.exe" -I ArtSource/PawnRush/Sfx/bake_carriage_yard.py
"""
import hashlib, os, wave
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '../../..'))
RAW = os.path.join(HERE, 'raw', 'CarriageYard')
OUT_REL = 'Assets/Resources/PawnRushCarriageYardSfx'
OUT = os.path.join(os.environ.get("RCY_SFX_ROOT", ROOT), OUT_REL)   # RCY_SFX_ROOT: a dry run elsewhere
EFFECTS = .8
CEILING = 10 ** (-1 / 20)
MOST = 8.

# family: takes, how many strikes to keep, longest strike (s), heard dB (the whole strike), the call's volume
HOOVES = {
    'HoofLight': dict(takes=['hoof_light_1', 'hoof_light_2', 'hoof_light_3', 'hoof_light_4'], keep=6, longest=.14, heard=-26, vol=1.),
    'HoofHeavy': dict(takes=['hoof_heavy_1', 'hoof_heavy_2', 'hoof_heavy_3', 'hoof_heavy_4'], keep=6, longest=.17, heard=-24, vol=1.),
}
# out name: take, where to look for the first hit (s), the latest end (s), fade (s), heard dB (loudest 400 ms), volume
ONE_SHOTS = {
    'HorseSnort0': dict(take='snort_1', frm=0., end=1.2, fade=.12, heard=-21, vol=1.),
    'HorseSnort1': dict(take='snort_4', frm=0., end=1.2, fade=.12, heard=-21, vol=1.),
    'HorseNeigh0': dict(take='neigh_3', frm=0., end=1.7, fade=.15, heard=-20, vol=1.),
    'HorseNeigh1': dict(take='neigh_4', frm=0., end=1.6, fade=.15, heard=-20, vol=1.),
    'CarriageHit0': dict(take='hit_3', frm=0., end=.6, fade=.08, heard=-19, vol=1.),
    'CarriageHit1': dict(take='hit_1', frm=0., end=.6, fade=.08, heard=-19, vol=1.),
    # the giant chest (SlamChest.Sound.cs, 승규 "6번 거대 짐"): set down, the wave out across the floor (under the
    # thud), its chains paying out over the 1.1 s it is lowered and winding in over the 1.4 s it is hoisted
    'SlamThud0': dict(take='slam_thud_1', frm=0., end=1.5, fade=.25, heard=-18, vol=1.),
    'SlamThud1': dict(take='slam_thud_4', frm=0., end=1.4, fade=.25, heard=-18, vol=1.),
    'SlamShock0': dict(take='slam_shock_4', frm=0., end=1.6, fade=.4, heard=-24, vol=1.),
    'SlamShock1': dict(take='slam_shock_2', frm=0., end=1.4, fade=.4, heard=-24, vol=1.),
    'ChainLower0': dict(take='chain_lower_4', frm=0., end=1.17, fade=.08, heard=-24, vol=1.),
    'ChainLower1': dict(take='chain_lower_3', frm=0., end=1.0, fade=.08, heard=-24, vol=1.),
    'ChainHoist0': dict(take='chain_hoist_1', frm=0., end=1.45, fade=.15, heard=-24, vol=1.),
    'ChainHoist1': dict(take='chain_hoist_2', frm=0., end=1.5, fade=.15, heard=-24, vol=1.),
    # the storage rows (StackRow.Sound.cs, 승규 "8번 짐더미 줄"): four cranes, a trip every ~2.5 s each, so kept under the
    # other machines - a stack set down in the gap, its lift and carry (lift .35 + carry .7 + lower .35 s)
    'StackSet0': dict(take='stack_set_1', frm=0., end=.6, fade=.12, heard=-22, vol=1.),
    'StackSet1': dict(take='stack_set_3', frm=0., end=.75, fade=.12, heard=-22, vol=1.),
    'StackMove0': dict(take='stack_move_3', frm=0., end=1.0, fade=.15, heard=-27, vol=1.),
    'StackMove1': dict(take='stack_move_2', frm=0., end=1.4, fade=.2, heard=-27, vol=1.),
    # the loft hoists (PalletHoist.Sound.cs, 승규 "9·10번 크레인·승강대"): the ropes over the 1.5 s swap, the pallets arriving
    'HoistMove0': dict(take='hoist_move_1', frm=0., end=1.75, fade=.2, heard=-25, vol=1.),
    'HoistMove1': dict(take='hoist_move_3', frm=0., end=1.8, fade=.2, heard=-25, vol=1.),
    'HoistLand0': dict(take='hoist_land_1', frm=0., end=.7, fade=.1, heard=-22, vol=1.),
    'HoistLand1': dict(take='hoist_land_3', frm=0., end=.75, fade=.1, heard=-22, vol=1.),
    # the outer gate (TimedGate.Sound.cs, 승규 "2·12번 성문"): its bolt, the hinges over its 2.5 s swing, the leaves at rest
    'GateOpen0': dict(take='gate_open_3', frm=0., end=3.0, fade=.3, heard=-20, vol=1.),
    'GateOpen1': dict(take='gate_open_4', frm=0., end=2.0, fade=.3, heard=-20, vol=1.),
    # hits (YardSoundscape.Hits.cs, 승규 "13번 타격음"): a crate, stack, pallet or timber load; a waterwheel's paddle
    'HitBump0': dict(take='hit_bump_1', frm=0., end=.6, fade=.1, heard=-20, vol=1.),
    'HitBump1': dict(take='hit_bump_2', frm=.6, end=1.25, fade=.1, heard=-20, vol=1.),
    'HitBump2': dict(take='hit_bump_3', frm=.15, end=.9, fade=.1, heard=-20, vol=1.),
    'HitPaddle0': dict(take='hit_paddle_1', frm=.2, end=.9, fade=.12, heard=-20, vol=1.),
    'HitPaddle1': dict(take='hit_paddle_3', frm=0., end=.7, fade=.12, heard=-20, vol=1.),
    'HitPaddle2': dict(take='hit_paddle_4', frm=.4, end=.95, fade=.12, heard=-20, vol=1.),
}


def load(name):
    w = wave.open(os.path.join(RAW, name + '.wav'))
    x = np.frombuffer(w.readframes(w.getnframes()), np.int16).astype(np.float64).reshape(-1, w.getnchannels()) / 32768
    return x, w.getframerate()


def mono(x):
    if x.shape[1] == 1: return x[:, 0]
    corr = np.corrcoef(x[:, 0], x[:, 1])[0, 1]
    if corr >= .2: return x.mean(1)
    return x[:, int((x[:, 1] ** 2).sum() > (x[:, 0] ** 2).sum())]


def env(m, sr, step=.005):
    k = int(step * sr)
    n = len(m) // k
    return 10 * np.log10(np.array([np.mean(m[i * k:(i + 1) * k] ** 2) for i in range(n)]) + 1e-12), k


def onsets(m, sr):
    """Frames (5 ms) rising 9 dB over the quietest of the last 60 ms, within 20 dB of the loudest, 80 ms apart."""
    e, k = env(m, sr)
    top, out = e.max(), []
    for i in range(1, len(e)):
        if e[i] - e[max(0, i - 12):i].min() >= 9 and e[i] > top - 20 and (not out or (i - out[-1]) * k / sr > .08):
            out.append(i)
    # the strike's start: 2 ms before its first 1 ms within 12 dB of the frame's loudest
    starts = []
    for i in out:
        a = max(0, (i - 2) * k)
        seg = m[a:(i + 2) * k]
        j = max(1, int(.001 * sr))
        f = np.array([np.mean(seg[p:p + j] ** 2) for p in range(0, len(seg) - j, j)])
        d = 10 * np.log10(f + 1e-12)
        first = int(np.argmax(d >= d.max() - 12))
        starts.append(max(0, a + first * j - int(.002 * sr)))
    return starts


def limit(y, sr):
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


def loud400(y, sr):
    k = int(.4 * sr)
    if len(y) <= k: return 10 * np.log10(np.mean(y ** 2) + 1e-12)
    return 10 * np.log10(np.convolve(y ** 2, np.ones(k) / k, 'valid').max() + 1e-12)


def whole(y, sr): return 10 * np.log10(np.mean(y ** 2) + 1e-12)


def level(y, sr, want, measure):
    g = 10 ** ((want - measure(y, sr)) / 20)
    for _ in range(3):
        out, took = limit(y * g, sr)
        g *= 10 ** ((want - measure(out, sr)) / 20)
    out, took = limit(y * g, sr)
    if took <= MOST: return out, took
    lo, hi = 0., g
    for _ in range(20):
        mid = (lo + hi) / 2
        if limit(y * mid, sr)[1] > MOST: hi = mid
        else: lo = mid
    return limit(y * lo, sr)


def fades(y, sr, fade):
    r = max(1, int(.001 * sr))
    y[:r] *= np.linspace(0, 1, r)
    f = min(len(y) // 2, int(fade * sr))
    y[-f:] *= np.cos(np.linspace(0, np.pi / 2, f)) ** 2
    return y


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
    path = os.path.join(os.environ.get('RCY_SFX_ROOT', ROOT), rel) + '.meta'
    if os.path.exists(path): return 'kept'
    g = hashlib.md5(('chessfight:' + rel).encode()).hexdigest()
    with open(path, 'w', newline='\n') as f: f.write(META.format(g=g))
    return 'new'


def to_file(heard, vol): return heard - 20 * np.log10(vol * EFFECTS)


def folder_meta():
    path = OUT + '.meta'
    if os.path.exists(path): return
    g = hashlib.md5(('chessfight:' + OUT_REL).encode()).hexdigest()
    with open(path, 'w', newline='\n') as f:
        f.write(f'fileFormatVersion: 2\nguid: {g}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')


os.makedirs(OUT, exist_ok=True)
folder_meta()
print('name\tfrom\tlength_s\theard_dB\tlimiter_dB\tpeak_dB\tmeta')
for fam, c in HOOVES.items():
    found = []
    for take in c['takes']:
        x, sr = load(take)
        m = mono(x)
        st = onsets(m, sr)
        for n, a in enumerate(st):
            b = min(a + int(c['longest'] * sr), (st[n + 1] - int(.005 * sr)) if n + 1 < len(st) else len(m))
            seg = m[a:b].copy()
            if len(seg) < int(.06 * sr): continue
            e, k = env(seg, sr)
            peak = e.max()
            tail = e[-4:].mean()            # what is left at the cut (the last 20 ms)
            found.append((peak - max(tail, peak - 40) + .5 * peak, take, a / sr, seg, sr, peak - tail))
    found.sort(key=lambda f: -f[0])
    for i, (score, take, at, seg, sr, decay) in enumerate(found[:c['keep']]):
        name = f'{fam}{i}'
        y = fades(seg, sr, .03)
        y, took = level(y, sr, to_file(c['heard'], c['vol']), whole)
        write(os.path.join(OUT, name + '.wav'), y, sr)
        print(f'{name}\t{take}@{at:.3f} (decay {decay:.0f} dB, of {len(found)})\t{len(y) / sr:.3f}\t'
              f'{whole(y, sr) + 20 * np.log10(c["vol"] * EFFECTS):.1f}\t{took:.1f}\t{20 * np.log10(np.abs(y).max()):.1f}\t{meta(name)}')
for name, c in ONE_SHOTS.items():
    x, sr = load(c['take'])
    m = mono(x)
    a0, b0 = int(c['frm'] * sr), min(len(m), int(c['end'] * sr))
    st = [s for s in onsets(m[a0:b0], sr)]
    a = a0 + (st[0] if st else 0)
    e, k = env(m[a:b0], sr)
    alive = np.nonzero(e > e.max() - 40)[0]
    b = a + (alive[-1] + 1) * k if len(alive) else b0
    y = fades(m[a:b].copy(), sr, c['fade'])
    y, took = level(y, sr, to_file(c['heard'], c['vol']), loud400)
    write(os.path.join(OUT, name + '.wav'), y, sr)
    print(f'{name}\t{c["take"]}@{a / sr:.3f}-{b / sr:.3f}\t{len(y) / sr:.3f}\t'
          f'{loud400(y, sr) + 20 * np.log10(c["vol"] * EFFECTS):.1f}\t{took:.1f}\t{20 * np.log10(np.abs(y).max()):.1f}\t{meta(name)}')

# Loops (the conveyor, 승규 "7번 컨베이어"): the effects tool gives 1 - 2 s takes, so a loop is several takes in a row,
# each trimmed (`trim` s off both ends), joined by XF s equal-power crossfades, its end crossfaded into its start:
# a seamless loop of ~6 s that does not repeat every 2 s. Levelled by its RMS to `heard` (R654 loops: -30 dB).
XF = .25
LOOPS = {
    'ConveyorBelt': dict(takes=['belt_1', 'belt_2', 'belt_3', 'belt_4'], trim=.05, heard=-30, vol=1.),   # CargoConveyor, always
    'ConveyorRide': dict(takes=['ride_1', 'ride_3', 'ride_2'], trim=.05, heard=-31, vol=1.),             # each load on the belt
    # the waterwheels and the water (YardSoundscape, 승규 "4·5번 물레방아·물")
    'WheelTurn': dict(takes=['wheel_1', 'wheel_3', 'wheel_2', 'wheel_4'], trim=.05, heard=-29, vol=1.),   # each wheel, at its axle
    'WaterPour': dict(takes=['pour_1', 'pour_3', 'pour_4', 'pour_2'], trim=.05, heard=-28, vol=1.),       # where water falls
    'WaterFlow': dict(takes=['flow_1', 'flow_2', 'flow_4', 'flow_3'], trim=.05, heard=-33, vol=1.),       # the canal, the pond
    'CraneSlew': dict(takes=['crane_4', 'crane_1', 'crane_3', 'crane_2'], trim=.05, heard=-29, vol=1.),   # a timber crane slewing
    'GatePush': dict(takes=['gate_push_1', 'gate_push_4', 'gate_push_2', 'gate_push_3'], trim=.05, heard=-27, vol=1.),   # the final gate pushed
}


def chain(parts, sr):
    n = int(XF * sr)
    t = (np.arange(n) + .5) / n
    up, down = np.sin(t * np.pi / 2), np.cos(t * np.pi / 2)
    out = parts[0].copy()
    for p in parts[1:]:
        out[-n:] = out[-n:] * down + p[:n] * up
        out = np.concatenate([out, p[n:]])
    # close the loop: the last XF into the first
    out[:n] = out[:n] * up + out[-n:] * down
    return out[:-n]


for name, c in LOOPS.items():
    parts, sr = [], 44100
    for take in c['takes']:
        x, sr = load(take)
        m = mono(x)
        k = int(c['trim'] * sr)
        parts.append(m[k:len(m) - k])
    y = chain(parts, sr)
    y, took = level(y, sr, to_file(c['heard'], c['vol']), whole)
    write(os.path.join(OUT, name + '.wav'), y, sr)
    print(f'{name}\t{"+".join(c["takes"])}\t{len(y) / sr:.3f}\t{whole(y, sr) + 20 * np.log10(c["vol"] * EFFECTS):.1f}'
          f'\t{took:.1f}\t{20 * np.log10(np.abs(y).max()):.1f}\t{meta(name)}\tjoin step {abs(y[0] - y[-1]):.4f}')
