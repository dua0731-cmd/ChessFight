"""2026-11-26 (R850): the last four free-plan clips (pawn dash / bump / crouch, SwordHit), retaken on the paid plan.

  python Tools/Sfx/Regen2026/r850.py OLD_DIR                     measure every take, write r850_picks.json
  python Tools/Sfx/Regen2026/r850.py OLD_DIR --install           also copy the closest takes into the game
  python Tools/Sfx/Regen2026/r850.py OLD_DIR --install SwordHit=2   take number 2 for that clip instead

Needs numpy, imageio-ffmpeg (or FFMPEG), opencv (build.py imports it) and miniaudio (sword_hit_thick.py).
OLD_DIR holds the four old files, a reference only, never committed. Takes are numbered in the order of
r850_sessions.json. Each is compared in the shape the game gets: the pawn clips as downloaded, SwordHit after
Tools/Sfx/SwordFightSwing/sword_hit_thick.py. Coin-ringing takes (coin.py) are never picked automatically.
"""
import json, os, shutil, subprocess, sys
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import process  # noqa: E402  (chdirs into Tools/Sfx/PawnRushSkills for its imports)
import similar  # noqa: E402

ROOT = process.ROOT
OUT = os.path.join(ROOT, 'ArtSource/SkillSfxRegen2026/out/R850')
THICK = os.path.join(ROOT, 'Tools/Sfx/SwordFightSwing/sword_hit_thick.py')
CLIPS = {'PawnDash': ('SharedSkills', 'SharedSkills/Sfx/PawnDash.mp3'),
         'PawnBump': ('SharedSkills', 'SharedSkills/Sfx/PawnBump.mp3'),
         'PawnCrouch': ('SharedSkills', 'SharedSkills/Sfx/PawnCrouch.mp3'),
         'SwordHit': ('SwordFightSkillSfx', 'SwordFightSkillSfx/SwordHit.wav')}


def load(path):
    raw = subprocess.run([process.FF, '-v', 'error', '-i', path, '-ac', '1', '-ar', '44100', '-f', 'f32le', '-'],
                         capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.float32).astype(np.float64)


def ring_ms(path):
    r = process.rings(process.trim(process.dc_block(process.load(path))))
    return round(max((c['n'] * 512 / process.SR for c in r), default=0.0) * 1000)


def main(old_dir, install, chosen):
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(HERE, 'r850_sessions.json'), encoding='utf-8') as f:
        sessions = json.load(f)['sessions']
    report = {}
    for name, (folder, asset) in CLIPS.items():
        old_path = os.path.join(old_dir, os.path.basename(asset))
        old = load(old_path)
        ext = os.path.splitext(asset)[1]
        rows = []
        gens = [s['generation'] for s in sessions.values() if s['clip'].endswith('/' + name)]
        for number, gen in enumerate(gens, 1):
            take = os.path.join(ROOT, 'ArtSource/SkillSfxRegen2026/takes', folder, 'R850', f'{name}_{gen}.mp3')
            out = os.path.join(OUT, f'{name}_{number}{ext}')
            if name == 'SwordHit':
                subprocess.run([sys.executable, THICK, take, out], check=True, capture_output=True)
            else:
                shutil.copyfile(take, out)
            x = load(out)
            d, b, e = similar.distance(old, x, name)
            rows.append(dict(number=number, take=os.path.relpath(take, ROOT).replace(os.sep, '/'), out=out,
                             distance=round(d, 1), bands=round(b, 1), envelope=round(e, 1),
                             len_s=round(len(x) / 44100, 2), coin_ring_ms=ring_ms(out)))
        ok = [r for r in rows if not r['coin_ring_ms']]
        best = min(ok, key=lambda r: r['distance']) if ok else None
        if name in chosen:
            best = rows[chosen[name] - 1]
        report[name] = dict(asset='Assets/Resources/' + asset, old_coin_ring_ms=ring_ms(old_path),
                            pick=best['number'] if best else None,
                            picked_by='user' if name in chosen else 'numbers',
                            takes=[{k: v for k, v in r.items() if k != 'out'} for r in rows])
        print(f"{name:11s} old ring {report[name]['old_coin_ring_ms']} ms, pick {report[name]['pick']}")
        for r in rows:
            print(f"   {r['number']}  distance {r['distance']:5.1f} (bands {r['bands']:4.1f}, env {r['envelope']:4.1f})"
                  f"  len {r['len_s']}  ring {r['coin_ring_ms']} ms")
        if install and best:
            dest = os.path.join(ROOT, 'Assets/Resources', asset)
            assert os.path.exists(dest + '.meta'), dest
            shutil.copyfile(best['out'], dest)
    with open(os.path.join(HERE, 'r850_picks.json'), 'w', encoding='utf-8') as f:
        json.dump(dict(generated='2026-11-26', request='R850', flow='tDDwKPGgDnbm5TjY3t1Z',
                       model='eleven_text_to_sound_v2', plan='ElevenLabs paid plan',
                       measure='similar.py distance to the old clip (lower is closer)', clips=report),
                  f, indent=1, ensure_ascii=False)


if __name__ == '__main__':
    args = sys.argv[2:]
    main(os.path.abspath(sys.argv[1]), '--install' in args,
         {a.split('=')[0]: int(a.split('=')[1]) for a in args if '=' in a})
