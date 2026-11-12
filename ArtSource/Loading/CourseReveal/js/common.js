// Shared data, timing helpers, sound and the player that drives each mockup stage.
// Every frame is a pure function of t (seconds since the host pressed start) and the course seed,
// the same rule the game uses so every PC shows the same frame at the same moment.

export const ZONES = [
  { ko: '바깥 뜰', en: 'OUTER CASTLE' },
  { ko: '아래 궁전', en: 'LOWER PALACE' },
  { ko: '위 궁전', en: 'UPPER PALACE' },
  { ko: '작전 회의실', en: 'WAR ROOM' },
];

// The seven slots of a round and what each may draw (Resources/Loading/PawnRushCourse.txt).
// Mini-games carry their concept art; the obstacle sections have no art yet, so they carry an emblem.
export const SLOTS = [
  { kind: 'obstacle', label: '장애물 1', zone: 0, opts: [
    { name: '왕실 수로 정원', emblem: 'canal' },
    { name: '왕실 마차뜰', emblem: 'carriage' } ] },
  { kind: 'game', label: '미니게임 1', zone: 0, opts: [
    { name: '해자 도개교', img: 'img/moat.jpg' },
    { name: '정문 쇠살문 누각', img: 'img/portcullis.jpg' },
    { name: '정문 의장 광장', img: 'img/parade.jpg' } ] },
  { kind: 'obstacle', label: '장애물 2', zone: 1, opts: [
    { name: '대계단 현관홀', emblem: 'stair' } ] },
  { kind: 'game', label: '미니게임 2', zone: 1, opts: [
    { name: '보수 중인 동관', img: 'img/repair.jpg' },
    { name: '왕궁 서고', img: 'img/library.jpg' },
    { name: '대무도회장', img: 'img/ballroom.jpg' },
    { name: '왕궁 세탁장', img: 'img/laundry.jpg' } ] },
  { kind: 'obstacle', label: '장애물 3', zone: 2, opts: [
    { name: '임시 바닥', emblem: 'planks' } ] },
  { kind: 'game', label: '미니게임 3', zone: 2, opts: [
    { name: '왕의 행차 회랑', img: 'img/procession.jpg' },
    { name: '국왕 체스 홀', img: 'img/chesshall.jpg' },
    { name: '국왕 침실', img: 'img/bedchamber.jpg' } ] },
  { kind: 'final', label: '결승', zone: 3, opts: [
    { name: '작전 회의실', emblem: 'crown' } ] },
];

export const TEAMS = {
  white: ['하얀번개', '앙파상', '캐슬링', '룩앤롤', '프로모션', '오프닝북'],
  black: ['흑단기사', '블랙폰', '갬빗', '포크왕', '스테일메이트', '엔드게임'],
};

// The course summary row: zone headers over seven chips (picture or emblem, number, name).
export function summaryHTML(course, zoneColors) {
  const spans = [[1, 3], [3, 5], [5, 7], [7, 8]];
  const heads = ZONES.map((z, i) => `<div class="zh" style="grid-column:${spans[i][0]}/${spans[i][1]};--zc:${zoneColors[i]}"><b>${z.ko}</b><span>${z.en}</span></div>`).join('');
  const chips = SLOTS.map((s, i) => {
    const o = s.opts[course[i]];
    return `<div class="chip" style="--zc:${zoneColors[s.zone]}"><div class="th">${o.img ? `<img src="${o.img}" alt="">` : emblemSVG(o.emblem)}</div><div class="cn"><em>${i + 1}</em><span>${o.name}</span></div></div>`;
  }).join('');
  return heads + chips;
}

export function mulberry32(seed) {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6D2B79F5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

export function drawCourse(seed) {
  const r = mulberry32(seed * 7919 + 17);
  return SLOTS.map(s => Math.floor(r() * s.opts.length) % s.opts.length);
}

export const clamp = (v, a, b) => Math.min(b, Math.max(a, v));
export const clamp01 = v => clamp(v, 0, 1);
export const lerp = (a, b, u) => a + (b - a) * u;
export const seg = (t, a, b) => clamp01((t - a) / (b - a));
export const E = {
  outCubic: u => 1 - Math.pow(1 - u, 3),
  outQuart: u => 1 - Math.pow(1 - u, 4),
  outQuint: u => 1 - Math.pow(1 - u, 5),
  inCubic: u => u * u * u,
  inOutCubic: u => (u < .5 ? 4 * u * u * u : 1 - Math.pow(-2 * u + 2, 3) / 2),
  inOutSine: u => -(Math.cos(Math.PI * u) - 1) / 2,
  smooth: u => u * u * (3 - 2 * u),
  outBack: (u, s = 1.9) => 1 + (s + 1) * Math.pow(u - 1, 3) + s * Math.pow(u - 1, 2),
  outElastic: u => (u === 0 || u === 1 ? u : Math.pow(2, -10 * u) * Math.sin((u * 10 - .75) * (2 * Math.PI) / 3) + 1),
};
// A decaying shake, deterministic in time: returns [x, y] in -1..1 scaled by `amp`.
export function shake(age, amp, dur = .32, seed = 1) {
  if (age < 0 || age > dur) return [0, 0];
  const k = Math.pow(1 - age / dur, 2) * amp;
  return [Math.sin(age * 91 + seed * 3.1) * k, Math.cos(age * 77 + seed * 1.7) * k];
}

// Reel position for a slot-machine roll: from 0 to `target` with a long quint ease-out.
export const reelPos = (u, target) => target * E.outQuint(clamp01(u));
// Times (relative to roll start) at which the reel crosses each whole step, for the ticks.
export function reelTicks(target, dur, minGap = .045) {
  const out = [];
  let last = -1;
  for (let k = 1; k <= target; k++) {
    const u = 1 - Math.pow(1 - k / target, 1 / 5);
    const t = u * dur;
    if (t - last >= minGap || k === target) { out.push({ t, last: k === target }); last = t; }
  }
  return out;
}

// Pictures still to pass at progress u (0..1) of a roll: a fast steady spin, then the last `slow`
// pictures crawl past and the reel stops dead at 0. The two parts meet at the same speed.
export function reelLeft(u, steps, slow = 3, split = .55) {
  u = clamp01(u);
  if (steps <= slow) return steps * Math.pow(1 - u, 3);
  if (u < split) return steps - (steps - slow) * (u / split);
  const k = (steps - slow) * (1 - split) / (split * slow);
  return slow * Math.pow(1 - (u - split) / (1 - split), k);
}
// Times (from roll start) at which each picture clicks past the window.
export function reelClicks(steps, dur, minGap = .04) {
  const out = [];
  let prev = steps, last = -1;
  for (let i = 1; i <= 2400; i++) {
    const u = i / 2400, c = Math.ceil(reelLeft(u, steps) - 1e-6);
    if (c < prev) {
      const t = u * dur;
      if (t - last >= minGap || c === 0) { out.push({ t, last: c === 0, left: c }); last = t; }
      prev = c;
    }
  }
  return out;
}

// Gold line emblems for the sections that have no art (and the "?" before a reveal).
export function emblemSVG(kind, stroke = 'currentColor') {
  const s = `fill="none" stroke="${stroke}" stroke-width="3.2" stroke-linecap="round" stroke-linejoin="round"`;
  const paths = {
    canal: `<path ${s} d="M14 56c6-5 12-5 18 0s12 5 18 0 12-5 18 0"/><path ${s} d="M14 67c6-5 12-5 18 0s12 5 18 0 12-5 18 0"/><path ${s} d="M22 46V34a19 19 0 0 1 38 0v12"/><path ${s} d="M16 46h50"/><path ${s} d="M30 46V36a11 11 0 0 1 22 0v10"/>`,
    carriage: `<path ${s} d="M22 52V32c0-5 4-9 9-9h20c5 0 9 4 9 9v20"/><path ${s} d="M17 52h48"/><circle ${s} cx="27" cy="60" r="8"/><circle ${s} cx="55" cy="60" r="8"/><path ${s} d="M33 23l8-8 8 8"/><path ${s} d="M35 34h12v10H35z"/>`,
    stair: `<path ${s} d="M14 66h12v-9h11v-9h11v-9h11v-9h9"/><path ${s} d="M14 66h54"/><path ${s} d="M64 30V18"/><path ${s} d="M64 18l-7 4 7 4"/>`,
    planks: `<path ${s} d="M12 44h58"/><path ${s} d="M16 37l18 4"/><path ${s} d="M38 39l10-3 20 6"/><path ${s} d="M20 52l-4 14M62 52l4 14M41 49v17"/><path ${s} d="M14 52h54"/>`,
    crown: `<path ${s} d="M17 58l-4-30 15 13 13-22 13 22 15-13-4 30z"/><path ${s} d="M17 66h48"/><circle ${s} cx="41" cy="16" r="3"/>`,
    question: `<path ${s} d="M31 30a10 10 0 1 1 15 8.6c-3 1.7-5 4.2-5 7.4v3"/><circle cx="41" cy="58" r="2.6" fill="${stroke}"/>`,
  };
  return `<svg viewBox="0 0 82 82" aria-hidden="true">${paths[kind] || paths.question}</svg>`;
}

// A chess pawn drawn in SVG (white = ivory lacquer, black = ebony), with stubby legs that can run.
export function pawnSVG(team, id) {
  const ivory = team === 'white';
  const g1 = ivory ? '#fffaf0' : '#5a4a40', g2 = ivory ? '#e2cfac' : '#1d1612', g3 = ivory ? '#b89a6c' : '#0b0807';
  const trim = ivory ? '#d9a441' : '#aeb6c2';
  return `<svg viewBox="0 0 60 80" class="pawn-svg" aria-hidden="true">
  <defs><radialGradient id="pg${id}" cx="38%" cy="30%" r="75%"><stop offset="0" stop-color="${g1}"/><stop offset=".55" stop-color="${g2}"/><stop offset="1" stop-color="${g3}"/></radialGradient></defs>
  <g class="legs"><rect class="leg l" x="20" y="62" width="7" height="14" rx="3.5" fill="#2a1d16"/><rect class="leg r" x="33" y="62" width="7" height="14" rx="3.5" fill="#2a1d16"/></g>
  <g class="body">
    <path d="M14 64c0-5 4-7 8-8 1-6 3-13 4-20h8c1 7 3 14 4 20 4 1 8 3 8 8z" fill="url(#pg${id})"/>
    <ellipse cx="30" cy="36" rx="12" ry="3.4" fill="url(#pg${id})"/>
    <ellipse cx="30" cy="36.2" rx="12" ry="3.4" fill="none" stroke="${trim}" stroke-width="1.4"/>
    <circle cx="30" cy="22" r="11" fill="url(#pg${id})"/>
    <ellipse cx="26" cy="17" rx="4" ry="2.6" fill="#fff" opacity="${ivory ? .55 : .22}"/>
  </g></svg>`;
}

// ---------------------------------------------------------------- sound
// Short synthesised cues: wood ticks for the reels, a low wooden slam for each reveal, whooshes and
// a warm low chord for the finish. No bells or coin jingles.
export const Sound = {
  ctx: null, master: null, run: null, on: false, noise: null,
  enable() {
    if (!this.ctx) {
      const C = window.AudioContext || window.webkitAudioContext;
      if (!C) return false;
      this.ctx = new C();
      const comp = this.ctx.createDynamicsCompressor();
      comp.threshold.value = -16; comp.ratio.value = 3.5;
      this.master = this.ctx.createGain(); this.master.gain.value = .85;
      this.master.connect(comp); comp.connect(this.ctx.destination);
      const n = this.ctx.sampleRate * 2, buf = this.ctx.createBuffer(1, n, this.ctx.sampleRate), d = buf.getChannelData(0);
      for (let i = 0; i < n; i++) d[i] = Math.random() * 2 - 1;
      this.noise = buf;
    }
    this.ctx.resume();
    this.on = true;
    return true;
  },
  disable() { this.on = false; this.stop(); },
  stop() {
    if (!this.run) return;
    const r = this.run; this.run = null;
    try { r.gain.setValueAtTime(0, this.ctx.currentTime); setTimeout(() => r.disconnect(), 200); } catch (e) { /* already gone */ }
  },
  // Schedules a stage's cues from `from` seconds into its timeline.
  play(cues, from = 0) {
    this.stop();
    if (!this.on || !this.ctx) return;
    this.run = this.ctx.createGain(); this.run.connect(this.master);
    const base = this.ctx.currentTime + .03 - from;
    for (const c of cues) if (c.t >= from - .005 && this[c.type]) this[c.type](base + c.t, c);
  },
  env(g, at, a, peak, d) {
    g.gain.setValueAtTime(.0001, at);
    g.gain.exponentialRampToValueAtTime(peak, at + a);
    g.gain.exponentialRampToValueAtTime(.0001, at + a + d);
  },
  osc(type, f0, f1, at, dur, peak, a = .003) {
    const o = this.ctx.createOscillator(), g = this.ctx.createGain();
    o.type = type; o.frequency.setValueAtTime(f0, at); o.frequency.exponentialRampToValueAtTime(f1, at + dur);
    this.env(g, at, a, peak, dur); o.connect(g).connect(this.run); o.start(at); o.stop(at + a + dur + .05);
  },
  burst(at, dur, peak, type, f, q = 1, f1) {
    const s = this.ctx.createBufferSource(); s.buffer = this.noise;
    const bq = this.ctx.createBiquadFilter(); bq.type = type; bq.frequency.setValueAtTime(f, at); bq.Q.value = q;
    if (f1) bq.frequency.exponentialRampToValueAtTime(f1, at + dur);
    const g = this.ctx.createGain(); this.env(g, at, Math.min(.02, dur * .3), peak, dur);
    s.connect(bq).connect(g).connect(this.run); s.start(at, Math.random()); s.stop(at + dur + .05);
  },
  tick(at, c) { const f = c.f || 700; this.osc('triangle', f, f * .72, at, .05, c.v || .14); this.burst(at, .018, (c.v || .14) * .5, 'bandpass', 1900, 1.4); },
  step(at, c) { this.osc('sine', 210, 120, at, .05, c.v || .05); },
  whoosh(at, c) { const d = c.d || .55; this.burst(at, d, c.v || .2, 'bandpass', c.down ? 2600 : 380, .9, c.down ? 380 : 2600); },
  riser(at, c) { const d = c.d || .8; this.burst(at, d, c.v || .08, 'bandpass', 300, 2.5, 1800); },
  slam(at, c) {
    const big = c.big;
    this.osc('sine', big ? 120 : 140, big ? 38 : 48, at, big ? .55 : .36, big ? .95 : .8, .004);
    this.osc('triangle', 230, 150, at, .08, .32);
    this.burst(at, big ? .22 : .12, big ? .5 : .35, 'lowpass', 1100);
    if (big) this.osc('sine', 55, 30, at, .9, .5, .01);
  },
  door(at, c) { this.slam(at, { big: c.big }); this.burst(at - .06, .18, .16, 'bandpass', 600, 2, 260); this.whoosh(at, { d: .5, v: .16, down: true }); },
  crown(at, c) {
    [-.36, -.24, -.12].forEach((o, i) => this.osc('sine', 95 - i * 6, 50, at + o, .22, .35 + i * .1, .004));
    this.slam(at, { big: true });
    const freqs = [73.4, 110, 146.8, 185, 220];
    for (const f of freqs) {
      const o = this.ctx.createOscillator(), lp = this.ctx.createBiquadFilter(), g = this.ctx.createGain();
      o.type = 'sawtooth'; o.frequency.value = f; o.detune.value = (Math.random() - .5) * 10;
      lp.type = 'lowpass'; lp.frequency.setValueAtTime(500, at); lp.frequency.linearRampToValueAtTime(1500, at + .6); lp.Q.value = .6;
      g.gain.setValueAtTime(.0001, at); g.gain.exponentialRampToValueAtTime(.075, at + .08); g.gain.setValueAtTime(.075, at + 1.3); g.gain.exponentialRampToValueAtTime(.0001, at + 2.6);
      o.connect(lp).connect(g).connect(this.run); o.start(at); o.stop(at + 2.7);
    }
  },
  go(at) { this.osc('triangle', 520, 380, at, .06, .16); this.osc('triangle', 780, 560, at + .1, .08, .18); this.whoosh(at + .08, { d: .45, v: .18 }); },
};

// ---------------------------------------------------------------- player
// Drives one stage: scales the 1280x720 design to the page, plays the timeline when the stage is on
// screen, loops with a new course, and offers replay, new course, sound and a time scrubber.
const players = [];
export class Player {
  constructor(section, scene) {
    this.section = section; this.scene = scene;
    this.wrap = section.querySelector('.stage-wrap');
    this.stage = section.querySelector('.stage');
    this.range = section.querySelector('input[type=range]');
    this.clock = section.querySelector('.clock');
    this.duration = scene.duration; this.hold = scene.hold || 3.5;
    this.range.max = (this.duration + 1).toFixed(2);
    this.seed = scene.firstSeed || (1 + Math.floor(Math.random() * 1e6));
    this.t = 0; this.playing = false; this.visible = false; this.started = false; this.last = 0;
    new ResizeObserver(() => this.fit()).observe(this.wrap);
    this.fit();
    section.querySelector('[data-act=replay]').addEventListener('click', () => this.play(0));
    section.querySelector('[data-act=seed]').addEventListener('click', () => { this.newSeed(); this.play(0); });
    const snd = section.querySelector('[data-act=sound]');
    snd.addEventListener('click', () => {
      if (Sound.on) Sound.disable(); else Sound.enable();
      document.querySelectorAll('[data-act=sound]').forEach(b => b.setAttribute('aria-pressed', Sound.on ? 'true' : 'false'));
      if (Sound.on) this.play(this.playing ? this.t : 0);
    });
    this.range.addEventListener('input', () => { this.playing = false; Sound.stop(); this.t = +this.range.value; this.render(0); });
    this.range.addEventListener('change', () => { /* stay paused where the user left it */ });
    new IntersectionObserver(es => {
      for (const e of es) {
        this.visible = e.intersectionRatio > .35;
        if (this.visible && !this.started) { this.started = true; this.scene.setSeed(this.seed); this.play(0); }
        if (!this.visible && this.playing && Sound.run && this.owner) Sound.stop();
      }
    }, { threshold: [0, .35, .6] }).observe(this.wrap);
    players.push(this);
    this.scene.setSeed(this.seed);
  }
  fit() {
    const k = this.wrap.clientWidth / 1280;
    this.stage.style.transform = `scale(${k})`;
    this.k = k;
    this.scene.resize?.(k);
  }
  newSeed() { this.seed = (this.seed * 48271 + 12345) % 2147483647 || 7; this.scene.setSeed(this.seed); }
  play(from) {
    this.t = from; this.playing = true; this.t0 = performance.now() / 1000 - from;
    for (const p of players) p.owner = false;
    this.owner = true;
    Sound.play(this.scene.cues(), from);
  }
  render(dt) {
    this.scene.frame(this.t, dt);
    this.range.value = Math.min(this.t, this.duration + 1).toFixed(2);
    this.clock.textContent = Math.min(this.t, 99).toFixed(1) + '초';
  }
  tick(now) {
    if (!this.visible || !this.started) return;
    const dt = Math.min(.05, now - (this.last || now)); this.last = now;
    if (this.playing) {
      this.t = now - this.t0;
      if (this.t > this.duration + this.hold) { this.newSeed(); this.play(0); }
    }
    this.render(dt);
  }
}
function loop(ms) {
  const now = ms / 1000;
  for (const p of players) p.tick(now);
  requestAnimationFrame(loop);
}
requestAnimationFrame(loop);

export function loadImage(src) {
  return new Promise((ok, fail) => { const im = new Image(); im.onload = () => ok(im); im.onerror = fail; im.src = src; });
}
