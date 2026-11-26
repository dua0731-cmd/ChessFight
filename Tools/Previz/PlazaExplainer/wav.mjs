// 16-bit mono WAV in and out, for the narration.
import fs from 'node:fs';

/** The samples of a 16-bit WAV and its rate. */
export function readWav(file) {
  const b = fs.readFileSync(file);
  let p = 12, rate = 44100, data = null;
  while (p + 8 <= b.length) {
    const id = b.toString('latin1', p, p + 4), size = b.readUInt32LE(p + 4);
    if (id === 'fmt ') rate = b.readUInt32LE(p + 12);
    if (id === 'data') { const n = Math.floor(Math.min(size, b.length - p - 8) / 2); data = new Int16Array(n); for (let i = 0; i < n; i++) data[i] = b.readInt16LE(p + 8 + i * 2); break; }
    p += 8 + size + (size & 1);
  }
  if (!data) throw new Error('no data chunk in ' + file);
  return { rate, data };
}

export function writeWav(file, data, rate = 44100) {
  const b = Buffer.alloc(44 + data.length * 2);
  b.write('RIFF', 0, 'latin1'); b.writeUInt32LE(36 + data.length * 2, 4); b.write('WAVEfmt ', 8, 'latin1');
  b.writeUInt32LE(16, 16); b.writeUInt16LE(1, 20); b.writeUInt16LE(1, 22); b.writeUInt32LE(rate, 24); b.writeUInt32LE(rate * 2, 28); b.writeUInt16LE(2, 32); b.writeUInt16LE(16, 34);
  b.write('data', 36, 'latin1'); b.writeUInt32LE(data.length * 2, 40);
  for (let i = 0; i < data.length; i++) b.writeInt16LE(data[i], 44 + i * 2);
  fs.writeFileSync(file, b);
}
