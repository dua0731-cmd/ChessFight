// Speaks every line of script.js with this PC's own Korean voice (Windows SAPI, offline) and measures it.
//   node tts.mjs [--rate 2] [--voice "Microsoft Heami Desktop"]
// Writes out/voice/<line id>.wav (44.1 kHz mono) and out/timing.json (line id -> seconds of speech), which the
// page and audio.mjs lay the timeline on.
import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { SCENES, lineId, layout } from './script.js';
import { readWav } from './wav.mjs';

const ROOT = path.dirname(fileURLToPath(import.meta.url)), OUT = path.join(ROOT, 'out'), VOICE = path.join(OUT, 'voice');
const arg = (k, d) => { const i = process.argv.indexOf('--' + k); return i > 0 ? process.argv[i + 1] : d; };
const RATE = Number(arg('rate', 2)), NAME = arg('voice', 'Microsoft Heami Desktop');
fs.mkdirSync(VOICE, { recursive: true });

const lines = SCENES.flatMap(sc => sc.lines.map((ln, i) => ({ id: lineId(sc, i), say: ln.say })));
fs.writeFileSync(path.join(VOICE, 'lines.json'), JSON.stringify(lines));
const q = s => s.replace(/'/g, "''");
fs.writeFileSync(path.join(VOICE, 'speak.ps1'), `
Add-Type -AssemblyName System.Speech
$s = New-Object System.Speech.Synthesis.SpeechSynthesizer
$s.SelectVoice('${q(NAME)}'); $s.Rate = ${RATE}
$fmt = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(44100, [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono)
$lines = Get-Content -Raw -Encoding UTF8 '${q(path.join(VOICE, 'lines.json'))}' | ConvertFrom-Json
foreach ($l in $lines) { $s.SetOutputToWaveFile((Join-Path '${q(VOICE)}' ($l.id + '.wav')), $fmt); $s.Speak($l.say) }
$s.SetOutputToNull(); $s.Dispose()
`);
const run = spawnSync('pwsh', ['-NoProfile', '-File', path.join(VOICE, 'speak.ps1')], { stdio: 'inherit' });
if (run.status !== 0) { console.error('the voice script failed'); process.exit(1); }

const voice = {};
for (const l of lines) {
  const { rate, data } = readWav(path.join(VOICE, l.id + '.wav'));
  let end = data.length - 1; while (end > 0 && Math.abs(data[end]) < 250) end--;   // the voice leaves a silent tail
  voice[l.id] = Math.round((end / rate + 0.08) * 1000) / 1000;
}
fs.writeFileSync(path.join(OUT, 'timing.json'), JSON.stringify({ rate: RATE, voice }, null, 1));
const L = layout(voice);
for (const s of L.scenes) console.log(`${s.id.padEnd(8)} ${s.start.toFixed(1).padStart(6)} s  +${s.dur.toFixed(1)} s`);
console.log(`total ${L.total.toFixed(1)} s (${Math.floor(L.total / 60)}:${String(Math.round(L.total % 60)).padStart(2, '0')}) at rate ${RATE}`);
