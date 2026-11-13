import json, os, sys, urllib.request
d = json.load(open("C:/tmp_sfx/status.json", encoding="utf-8"))
names = [("Heavy thick iron chain whipped", "ChainThrow"), ("Heavy thick iron chain running", "ChainRun"),
         ("Heavy iron hook catching", "ChainBite"), ("Thick iron chain yanked", "ChainPull"),
         ("Heavy iron chain hanging", "ChainOffer"), ("Hand grabbing", "ChainGrab"), ("Wooden toy figure dropped", "ChainLand"),
         ("Heavy iron chain reeled", "ChainRewind"), ("Taut heavy iron chain", "ChainBreak"), ("Loud muffled wooden knock", "SkillFail")]
seen = {}
done_ids = set()
for g in d["generations"]:
    if g["id"] in done_ids:
        continue
    done_ids.add(g["id"])
    name = next((n for p, n in names if g["prompt"].startswith(p)), "?")
    if g["status"] != "completed" or not g.get("content_url"):
        print("not ready", name, g["status"])
        continue
    seen[name] = seen.get(name, 0) + 1
    path = f"C:/tmp_sfx/{name}_{seen[name]}.mp3"
    if not os.path.exists(path):
        urllib.request.urlretrieve(g["content_url"], path)
    print("got", path, os.path.getsize(path))
