# Builds the Sword Fight edge-skill SFX pick page: spec.json + page/sfx/<skill>_<letter>.mp3 -> page/index.html.
# Per sound: length, an 96-point peak line for the waveform, and the gain that brings its loudest 0.3 s to -15 dB (<= 8x).
import json, os, subprocess, sys
import numpy as np, imageio_ffmpeg

root = sys.argv[1]
spec = json.load(open(os.path.join(root, 'spec.json'), encoding='utf-8'))
ff = imageio_ffmpeg.get_ffmpeg_exe()
SR = 22050
data = []
for sk in spec['skills']:
    row = {'id': sk['id'], 'name': sk['name'], 'moment': sk['moment'], 'sounds': []}
    for L in 'ABCDE':
        f = f"sfx/{sk['id']}_{L}.mp3"
        p = os.path.join(root, 'page', f)
        if not os.path.exists(p):
            row['sounds'].append({'L': L, 'file': None, 'prompt': sk['prompts'][L]}); continue
        raw = subprocess.run([ff, '-v', 'quiet', '-i', p, '-f', 'f32le', '-ac', '1', '-ar', str(SR), '-'], capture_output=True).stdout
        x = np.frombuffer(raw, np.float32).astype(np.float64)
        w = int(SR * 0.3)
        rms = np.sqrt((np.convolve(x * x, np.ones(w), 'valid') / w).max()) if len(x) > w else np.sqrt(np.mean(x * x))
        gain = float(min(8.0, 10 ** (-15 / 20) / max(rms, 1e-5), 0.98 / max(np.abs(x).max(), 1e-5)))
        n = 96
        peaks = [round(float(np.abs(c).max()) * gain, 3) if len(c) else 0 for c in np.array_split(x, n)]
        row['sounds'].append({'L': L, 'file': f, 'len': round(len(x) / SR, 2), 'gain': round(gain, 3), 'peaks': peaks, 'prompt': sk['prompts'][L]})
    data.append(row)

tpl = open(os.path.join(os.path.dirname(__file__), 'edgepage.html'), encoding='utf-8').read()
html = tpl.replace('/*DATA*/null', json.dumps({'directions': spec['directions'], 'skills': data}, ensure_ascii=False))
open(os.path.join(root, 'page', 'index.html'), 'w', encoding='utf-8').write(html)
print('ok', sum(1 for r in data for s in r['sounds'] if s['file']), 'sounds')
