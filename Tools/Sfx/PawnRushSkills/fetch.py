# Pulls the generated mp3s out of saved status results: finds every signed content URL, maps its session id
# (the path segment after content_generation/) to a cue through sessions.json, downloads what is missing.
#   python fetch.py <status-result-file> [...]
import json, os, re, sys, urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
sessions = json.load(open(os.path.join(HERE, "sessions.json"), encoding="utf-8"))
by_session = {v: k for k, v in sessions.items()}
url_re = re.compile(r'https://storage\.googleapis\.com/xi-backend/[^"\s]*?/content_generation/([A-Za-z0-9]+)/([A-Za-z0-9]+)/content\.mp3\?[^"\s]+')

found, got, missing = {}, [], []
for path in sys.argv[1:]:
    text = open(path, encoding="utf-8").read()
    for m in url_re.finditer(text):
        found.setdefault(m.group(1), m.group(0).replace("\\u0026", "&"))

os.makedirs(os.path.join(HERE, "gen", "alt"), exist_ok=True)
for sid, url in found.items():
    key = by_session.get(sid)
    if key is None:
        missing.append(sid)
        continue
    d, skill, cue = key.split(".", 2)
    if "#" in cue:
        cue, n = cue.split("#")
        out = os.path.join(HERE, "gen", "alt", f"{d}_{skill}_{cue}_{n}.mp3")
    else:
        out = os.path.join(HERE, "gen", f"{d}_{skill}_{cue}.mp3")
    if os.path.exists(out) and os.path.getsize(out) > 0:
        continue
    with urllib.request.urlopen(url, timeout=60) as r, open(out, "wb") as f:
        f.write(r.read())
    got.append((key, os.path.getsize(out)))

for k, n in sorted(got):
    print(f"got {k}: {n} bytes")
if missing:
    print("sessions not in sessions.json:", missing)
have = sorted(os.listdir(os.path.join(HERE, "gen")))
print(len([h for h in have if h.endswith(".mp3")]), "cue files in gen/")
