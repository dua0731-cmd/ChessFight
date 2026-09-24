# Writes a .meta with a deterministic GUID for each path given, skipping any that
# already has one. Use it for files added outside Unity (scripts, docs under Assets).
# Usage from the project root: python3 Tools/Generators/mkmeta.py Assets/Scripts/Core/New.cs
import sys, uuid, os, hashlib
def guid_for(path):
    # Deterministic per path so reruns keep the same GUID.
    return hashlib.md5(("chessfight:"+path).encode()).hexdigest()
for path in sys.argv[1:]:
    meta = path + ".meta"
    if os.path.exists(meta):
        print("skip", meta); continue
    g = guid_for(path)
    body = "fileFormatVersion: 2\nguid: %s\n" % g
    if os.path.isdir(path):
        body += "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    open(meta, "w").write(body)
    print(g, meta)
