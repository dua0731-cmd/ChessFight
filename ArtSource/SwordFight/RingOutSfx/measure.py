# R869: the 장외! stamp sound takes (raw/<A-D>_<1-4>.mp3, ElevenLabs flow qtXZXcO2Bh2qC9SI3wsr): each one's numbers for the
# listening page - peak, where the hit is, how long it rings on (to -40 dB) - into report.json. The AI cannot listen.
#   uv run --no-project --python "C:/Program Files/Blender Foundation/Blender 5.2/5.2/python/bin/python.exe" --with miniaudio --with numpy python -I measure.py <this folder>
import os, sys, json, numpy as np, miniaudio
S = sys.argv[1]; SR = 44100; out = {}
for name in sorted(os.listdir(os.path.join(S, 'raw'))):
    if not name.endswith('.mp3'): continue
    d = miniaudio.decode_file(os.path.join(S, 'raw', name), output_format=miniaudio.SampleFormat.FLOAT32, nchannels=1, sample_rate=SR)
    x = np.frombuffer(d.samples, dtype=np.float32).astype(np.float64)
    n = int(SR * .005); e = np.sqrt(np.convolve(x * x, np.ones(n) / n, mode='same') + 1e-12)
    top = e.max(); hit = int(np.argmax(e)); onset = int(np.where(e > top * .1)[0][0])
    after = np.where(e[hit:] < top * 10 ** (-40 / 20))[0]
    tail = (after[0] if len(after) else len(x) - hit) / SR
    out[name[:-4]] = dict(peak=round(20 * np.log10(max(np.abs(x).max(), 1e-9)), 1), onset=round(onset / SR, 3), hit=round(hit / SR, 3), tail=round(tail, 2), length=round(len(x) / SR, 2))
json.dump(out, open(os.path.join(S, 'report.json'), 'w'), indent=1)
for k, v in out.items(): print(k, v)
