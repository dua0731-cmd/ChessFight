"""2026-11-26 (R845): download the regenerated skill SFX takes.

  python3 -I Tools/Sfx/Regen2026/fetch.py urls.txt

urls.txt has one take per line: "<session id> <generation id> <signature>", copied from a
creative_get_flow_run_status result (every content_url of the flow shares the workspace, KeyName and the Expires
below; the signed links expire after about two hours). sessions.json maps each session id to "<folder>/<Name>".
Each take is saved as ArtSource/SkillSfxRegen2026/takes/<folder>/<Name>_<generation id>.mp3; files already there
are skipped. Needs elevenlabsusercontent.com through the proxy (the "ChessFight" cloud environment allows it).
"""
import json, os, sys, urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '../../..'))
TAKES = os.path.join(ROOT, 'ArtSource/SkillSfxRegen2026/takes')
WS = '1a95f9f757f44eee9c36e1a482a4c2d0'
EXPIRES = sys.argv[2] if len(sys.argv) > 2 else '1795687200'
sessions = json.load(open(os.path.join(HERE, 'sessions.json'), encoding='utf-8'))
for line in open(sys.argv[1], encoding='utf-8'):
    parts = line.split()
    if len(parts) != 3:
        continue
    sid, gid, sig = parts
    name = sessions.get(sid)
    if not name:
        print('unknown session', sid); continue
    out = os.path.join(TAKES, f'{name}_{gid}.mp3')
    if os.path.exists(out) and os.path.getsize(out) > 0:
        continue
    os.makedirs(os.path.dirname(out), exist_ok=True)
    url = (f'https://elevenlabsusercontent.com/database/workspace/{WS}/content_generation/{sid}/{gid}/content.mp3'
           f'?Expires={EXPIRES}&KeyName=assets-cdn-key-1&Signature={sig}')
    urllib.request.urlretrieve(url, out)
    print('got', os.path.relpath(out, ROOT), os.path.getsize(out))
