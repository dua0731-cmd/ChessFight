"""2026-11-26 (R847): Pawn Rush rook / bishop / knight clips, retaken closer to the old (free-plan) clips.

  python3 Tools/Sfx/Regen2026/r847.py OLD_DIR            measure, pick, write r847_picks.json
  python3 Tools/Sfx/Regen2026/r847.py OLD_DIR --install  also copy the picks over Assets/Resources/PawnRushSkillSfx

OLD_DIR holds the old clips (git show 89314acf:Assets/Resources/PawnRushSkillSfx/<name>.mp3 | git lfs smudge) -
a reference only, never shipped. Takes: ArtSource/SkillSfxRegen2026/takes/PawnRushSkillSfx/R847/<name>_<gen>.mp3
(flow XXLmzf1PbQplyMZT6fjo). Each take gets the old clip's post-processing (process.py chain / export_unity), the
coin-ring check, then similar.distance to the old clip. The R845 clip in the game now is measured the same way and
kept when no new take is closer, so a clip only changes when it moves toward the old sound.
"""
import glob, json, os, shutil, sys
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import process  # noqa: E402  (chdirs into Tools/Sfx/PawnRushSkills for its imports)
import similar  # noqa: E402

ROOT = process.ROOT
TAKES = os.path.join(ROOT, 'ArtSource/SkillSfxRegen2026/takes/PawnRushSkillSfx/R847')
GAME = os.path.join(ROOT, 'Assets/Resources/PawnRushSkillSfx')
OUT = os.path.join(ROOT, 'ArtSource/SkillSfxRegen2026/out/R847')
NAMES = ('ChainThrow ChainBite ChainPull ChainOffer ChainGrab ChainLand ChainRewind BishopRopeThrow BishopRopeHook '
         'BishopRopeStrain BishopRopeReel BishopRopeRelease BishopRopeSnap RookTick KnightLeap KnightLand KnightStomp').split()
KIND = {p['name']: p['kind'] for p in process.plan if p['folder'] == 'PawnRushSkillSfx'}


def processed(take, name):
    kind = KIND[name]
    if kind == 'chain':
        return process.chain(take, name)
    return process.export_unity(take, name)


def main(old_dir, install):
    os.makedirs(OUT, exist_ok=True)
    report = {}
    for name in NAMES:
        old = similar.load(os.path.join(old_dir, name + '.mp3'))
        game = os.path.join(GAME, name + '.mp3')
        now_d = similar.distance(old, similar.load(game), name)[0]
        rows = []
        for take in sorted(glob.glob(os.path.join(TAKES, f'{name}_*.mp3'))):
            x = processed(take, name)
            tmp = os.path.join(OUT, os.path.basename(take))
            process.save(x, tmp, 128 if KIND[name] == 'chain' else 160)
            r = process.rings(process.trim(process.dc_block(x)))
            ring = max((c['n'] * 512 / process.SR for c in r), default=0.0)
            d = similar.distance(old, similar.load(tmp), name)[0]
            rows.append(dict(take=os.path.relpath(take, ROOT), out=tmp, distance=round(d, 1), coin_ring_ms=round(ring * 1000)))
        ok = [r for r in rows if not r['coin_ring_ms']]
        best = min(ok, key=lambda r: r['distance']) if ok else None
        change = best is not None and best['distance'] < now_d
        report[name] = dict(kind=KIND[name], r845_distance=round(now_d, 1), takes=[{k: v for k, v in r.items() if k != 'out'} for r in rows],
                            pick=best['take'] if change else 'keep R845 clip', pick_distance=best['distance'] if change else round(now_d, 1))
        print(f"{name:18s} R845 {now_d:5.1f} -> {'%5.1f %s' % (best['distance'], os.path.basename(best['take'])) if change else 'keep'}"
              f"   takes: {' '.join(str(r['distance']) + ('*ring' if r['coin_ring_ms'] else '') for r in rows)}")
        if change and install:
            assert os.path.exists(game + '.meta'), game
            shutil.copyfile(best['out'], game)
    json.dump(dict(generated='2026-11-26', request='R847', flow='XXLmzf1PbQplyMZT6fjo', model='eleven_text_to_sound_v2',
                   plan='ElevenLabs paid plan', measure='similar.py distance to the old clip (lower is closer)', clips=report),
              open(os.path.join(HERE, 'r847_picks.json'), 'w'), indent=1, ensure_ascii=False)


if __name__ == '__main__':
    main(sys.argv[1], '--install' in sys.argv)
