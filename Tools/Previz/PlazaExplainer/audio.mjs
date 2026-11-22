// Puts the sound on the recorded film: the narration lines where the timeline says, and the game's own lobby
// music quietly under them.
//   node audio.mjs --video out/video.mp4 --out out/plaza-explainer.mp4 [--no-music] [--no-voice] [--music-db -21]
// Needs out/voice/*.wav and out/timing.json (tts.mjs) and the film recorded with the same timing.
import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { layout } from './script.js';
import { readWav, writeWav } from './wav.mjs';

const ROOT = path.dirname(fileURLToPath(import.meta.url)), OUT = path.join(ROOT, 'out');
const FFMPEG = 'C:/Program Files/Krita (x64)/bin/ffmpeg.exe';
const MUSIC = path.resolve(ROOT, '../../../Assets/Resources/Lobby/Music');
const has = k => process.argv.includes('--' + k), arg = (k, d) => { const i = process.argv.indexOf('--' + k); return i > 0 ? process.argv[i + 1] : d; };
const video = path.resolve(ROOT, arg('video', 'out/video.mp4')), out = path.resolve(ROOT, arg('out', 'out/plaza-explainer.mp4'));

const L = layout(JSON.parse(fs.readFileSync(path.join(OUT, 'timing.json'), 'utf8')).voice), RATE = 44100;
const track = new Float32Array(Math.ceil(L.total * RATE));
if (!has('no-voice')) for (const s of L.scenes) for (const ln of s.lines) {
  const { rate, data } = readWav(path.join(OUT, 'voice', ln.id + '.wav'));
  if (rate !== RATE) throw new Error(`${ln.id}.wav is ${rate} Hz`);
  const at = Math.round((s.start + ln.t0) * RATE);
  for (let i = 0; i < data.length && at + i < track.length; i++) track[at + i] += data[i];
}
let peak = 1, sum = 0;
for (const v of track) { peak = Math.max(peak, Math.abs(v)); sum += v * v; }
const gain = 23000 / peak, pcm = new Int16Array(track.length);      // about -3 dBFS: room for the music on top
for (let i = 0; i < track.length; i++) pcm[i] = Math.round(track[i] * gain);
writeWav(path.join(OUT, 'voice.wav'), pcm, RATE);
console.log(`voice: ${L.total.toFixed(1)} s, peak ${(20 * Math.log10(peak / 32768)).toFixed(1)} dBFS before levelling`);

/** A file's mean volume in dB, by ffmpeg's volumedetect. */
const meanDb = file => { const r = spawnSync(FFMPEG, ['-hide_banner', '-i', file, '-af', 'volumedetect', '-f', 'null', '-'], { encoding: 'utf8' }); return Number(/mean_volume:\s*(-?[\d.]+)/.exec(r.stderr)?.[1] ?? NaN); };
const T = L.total.toFixed(3), args = ['-y', '-loglevel', 'error', '-i', video, '-i', path.join(OUT, 'voice.wav')];
let filter = '[1:a]aformat=sample_rates=48000:channel_layouts=stereo[v]';
if (has('no-music')) filter += ';[v]anull[a]';
else {
  const intro = path.join(MUSIC, 'LobbyMusic.wav'), loop = path.join(MUSIC, 'LobbyMusicLoop.wav');
  // how far the music sits under the voice: the voice's mean includes its pauses, so the gap is wide
  const want = Number(arg('music-db', -21)), v = has('no-voice') ? -20 : meanDb(path.join(OUT, 'voice.wav')), m = meanDb(loop), g = (v + want - m).toFixed(1);
  console.log(`voice mean ${v} dB, music mean ${m} dB -> music gain ${g} dB`);
  args.push('-i', intro, '-stream_loop', '4', '-i', loop);
  filter += `;[2:a][3:a]concat=n=2:v=0:a=1,aformat=sample_rates=48000:channel_layouts=stereo,atrim=0:${T},volume=${g}dB,afade=t=in:d=1.5,afade=t=out:st=${(L.total - 3.5).toFixed(3)}:d=3.5[m];[v][m]amix=inputs=2:normalize=0:duration=first[a]`;
}
args.push('-filter_complex', filter, '-map', '0:v', '-map', '[a]', '-c:v', 'copy', '-c:a', 'aac', '-b:a', '128k', '-t', T, '-movflags', '+faststart', out);
const r = spawnSync(FFMPEG, args, { stdio: 'inherit' });
if (r.status !== 0) { console.error('ffmpeg failed'); process.exit(1); }
console.log(`${out}: ${(fs.statSync(out).size / 1048576).toFixed(1)} MiB`);
