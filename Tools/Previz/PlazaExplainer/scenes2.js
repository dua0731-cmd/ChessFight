// Plaza explainer - the scenes, second half: emotes, accessories, selling, minigames, friends, the order, what to decide.
import { el, show, hide, at, prog, smooth, easeOut, back, lerp, clamp, win, rng } from './kit.js';
import { HALL, padAt, NAME, HEIGHT } from './world.js';
import { pin, tag, bubble, cam, behind, look, path, hop, LOOK, padsState, padHops } from './ui.js';

/** Things flying out of a point, again every `period` seconds: letters and sparks around an emote. */
function burst(parts, t, t0, t1, ox, oy, period = 1.3) {
  parts.forEach((e, i) => {
    const r = rng(i + 3), ang = (i / parts.length) * Math.PI * 2 + r() * 0.5, far = 150 + r() * 170, lag = r() * 0.35;
    const ph = ((t - t0 - lag) % period + period) % period / period, on = t > t0 + lag && t < t1;
    if (!on) { hide(e); return; }
    const d = 60 + far * easeOut(ph);
    at(e, ox + Math.cos(ang) * d, oy - Math.sin(ang) * d * 0.8);
    e.style.visibility = 'visible'; e.style.opacity = (1 - prog(ph, 0.55, 1)).toFixed(3);
    e.style.transform = `translate(-50%, -50%) rotate(${(r() - 0.5) * 50}deg) scale(${(0.6 + 0.7 * easeOut(prog(ph, 0, 0.3))).toFixed(3)})`;
  });
}
const sparks = (root, n = 12) => Array.from({ length: n }, (_, i) => el('div', 'disp', i % 2 ? '✦' : 'ㅋ', root, { fontSize: i % 2 ? 46 : 64, color: i % 2 ? 'var(--brass)' : 'var(--wine)' }));

export function build2(world, { A, B, noInstall }) {
  const S = {}, w = world;

  // ---------- 3 emotes ----------
  S.emote = {
    make(root) {
      this.head = el('div', 'disp h2', '하나는 세 겹', root, { left: 1040, top: 120 });
      this.cards = [['① 몸 동작', '팔 · 가슴 · 머리로 2~3초'], ['② 이모티콘', '머리 위 말풍선'], ['③ 이펙트 · 소리', '한 번 터지는 효과']]
        .map(([a, b], i) => el('div', 'card', `<div class="disp h3">${a}</div><div class="soft">${b}</div>`, root, { left: 1040, top: 250 + i * 172, width: 700 }));
      this.brace = el('div', '', '', root, { left: 996, top: 244, width: 22, height: 494, borderLeft: '8px solid var(--wine)', borderTop: '8px solid var(--wine)', borderBottom: '8px solid var(--wine)', borderRadius: '16px 0 0 16px' });
      this.sum = el('div', 'chip winefill', '= 감정표현 1개 · 파는 단위', root, { left: 1040, top: 780, fontSize: 44 });
      this.fx = sparks(root); this.bub = bubble(root, '🤣');
      // two grades
      this.lab = [el('div', 'card', '<div class="disp h3 teal">스티커</div><div>달리면서 · 이모티콘과 소리</div>', root, { left: 232, top: 730, width: 560, textAlign: 'center' }),
        el('div', 'card', '<div class="disp h3 wine">동작</div><div>멈춰야 나옴 · 세 겹 전부</div>', root, { left: 1128, top: 730, width: 560, textAlign: 'center' })];
      this.fxB = sparks(root, 10); this.bubA = bubble(root, '+', 'sym'); this.bubB = bubble(root, '😜');
      this.track = el('div', '', '', root, { left: 360, top: 800, width: 1200, height: 14, background: '#cdbb9c', borderRadius: 7 });
      this.goal = el('div', '', '🏁', root, { left: 1570, top: 764, fontSize: 64 });
      this.dotA = el('div', 'chip tealfill', '스티커', root, { top: 728, fontSize: 30, padding: '6px 22px' });
      this.dotB = el('div', 'chip winefill', '동작', root, { top: 828, fontSize: 30, padding: '6px 22px' });
      this.lose = el('div', 'red', '멈춘 만큼 뒤처집니다', root, { top: 834, fontSize: 34, fontWeight: 700, whiteSpace: 'nowrap' });
      // the opponent's screen
      this.prompt = el('div', 'card', '<span class="key on">T</span> 놀리기', root, { left: 830, bottom: 176, fontSize: 40 });
      this.bubMe = bubble(root, '#', 'sym');
      this.mine = el('div', 'chip fill', '내 화면', root, { left: 60, top: 124 });
      this.frame = el('div', 'frame', '', root);
      this.theirs = el('div', 'chip winefill', '상대의 화면', root, { left: 60, top: 124 });
      this.edge = el('div', 'row', '<div class="who" style="font-size:38px;padding:6px 18px">JY</div><div class="bubble notail sym" style="position:static">#</div><div class="disp" style="font-size:96px;color:var(--wine);line-height:1">▶</div>', root, { right: 44, top: 380 });
      // chess symbols as emoticons
      this.symTitle = el('div', 'disp h2', '이모티콘에는 체스 기보 기호도', root, { left: 0, top: 120, width: 1920, textAlign: 'center' });
      this.syms = [['+', '체크', '쫓아간다'], ['#', '체크메이트', '끝났다'], ['½', '무승부', '화해'], ['1-0', '승리', '자랑']].map(([s, a, b], i) =>
        el('div', '', `<div class="bubble notail sym" style="position:static;margin:0 auto;width:fit-content">${s}</div><div class="disp h3" style="margin-top:18px">${a}</div><div class="soft">${b}</div>`, root, { left: 230 + i * 380, top: 270, width: 320, textAlign: 'center' }));
      this.faces = el('div', 'row', ['🤣', '😜', '😴', '👋'].map(f => `<div class="bubble notail" style="position:static;height:96px;min-width:96px;font-size:58px">${f}</div>`).join('') + '<span class="soft" style="margin-left:14px">얼굴 이모티콘과 섞어 씁니다</span>', root, { left: 0, top: 560, width: 1920, justifyContent: 'center' });
      this.stamp = el('div', 'soft small', '스킬 평가 도장( !! · ?? )은 시스템이 박는 글자, 감정표현은 사람이 보내는 말풍선으로 구분합니다', root, { left: 0, top: 690, width: 1920, textAlign: 'center' });
      this.guards = el('div', 'row', '<div class="chip">⏱ 쿨다운</div><div class="chip">🙈 상대 감정표현 끄기</div><div class="chip">글자 없음</div>', root, { left: 0, top: 780, width: 1920, justifyContent: 'center', gap: '28px', fontSize: 40 });
    },
    draw(t, c) {
      const all = [this.head, ...this.cards, this.brace, this.sum, this.bub, ...this.lab, this.bubA, this.bubB, this.track, this.goal, this.dotA, this.dotB, this.lose, this.prompt, this.bubMe, this.mine, this.frame, this.theirs, this.edge, this.symTitle, ...this.syms, this.faces, this.stamp, this.guards, ...this.fx, ...this.fxB];
      for (const e of all) hide(e);
      const P = w.studio.P, t2 = c.at(2), t3 = c.at(3), t4 = c.at(4), t5 = c.at(5);
      if (t < t2) {                                   // one emote, layer by layer
        const body = c.at(0) + 2.6, emo = c.at(0) + 4.0, fx = c.at(0) + 5.9;
        P.floor.scale.setScalar(2.2); P.rim.scale.setScalar(2.2);
        w.view(0.98, 1.2, 4.7, 0.98, 0.8, 0, 30);
        B('e1').wear(LOOK.me).place(0, 0, 0, 0.15).pose({ t, emote: 'laugh', w: prog(t, body, body + 0.35) });
        show(this.head, t, 0.5, t2 - 0.1);
        [body, emo, fx].forEach((x, i) => show(this.cards[i], t, x, t2 - 0.1, { dx: 40, dy: 0 }));
        pin(w, this.bub, 0, 1.42, 0, t, emo, t2 - 0.1);
        const o = w.project(0, 0.8, 0); burst(this.fx, t, fx, t2 - 0.15, o.x, o.y);
        show(this.brace, t, c.at(1) + 0.2, t2 - 0.1, { dx: 20, dy: 0 }); show(this.sum, t, c.at(1) + 0.4, t2 - 0.1, { pop: true, s: 0.7 });
        return 'studio';
      }
      if (t < t4) {                                   // two grades, and what the big one costs
        const r0 = t3 + 0.4, stopA = r0 + 1.2, stopB = r0 + 3.7, inRace = t >= r0;
        const emoting = win(t, t2 + 3.6, t2 + 6.4, 0.3, 0.3) + win(t, stopA, stopB, 0.3, 0.3);
        P.floor.scale.setScalar(3.9); P.rim.scale.setScalar(3.9);
        w.view(0, 1.5, 7.2, 0, 0.75, 0, 30);
        P.lane.visible = true; P.lane.position.set(-1.6, 0, 0); P.lane.rotation.y = Math.PI / 2;
        P.stripes.forEach((s, i) => { const z = ((i * 0.6 - t * 3.2) % 4.2 + 4.2) % 4.2 - 2.1; s.position.z = z; s.material.opacity = 0.9 * (1 - Math.abs(z) / 2.1); });
        B('e1').wear(LOOK.me).place(-1.6, 0, 0, Math.PI / 2 - 0.5).pose({ t, walk: 1, phase: t * 12 });
        B('e2').wear(LOOK.f1).place(1.6, 0, 0, -0.2).pose({ t, seed: 1, emote: 'taunt', w: Math.min(1, emoting) });
        const tb = ((t - t2 - 0.9) % 2.7 + 2.7) % 2.7;
        if (t > t2 + 0.9) pin(w, this.bubA, -1.6, 1.45, 0, tb, 0, 1.6);
        if (emoting > 0.05) { pin(w, this.bubB, 1.6, 1.5, 0, t, t < t3 ? t2 + 3.6 : stopA, t < t3 ? t2 + 6.4 : stopB); const o = w.project(1.6, 0.85, 0); burst(this.fxB, t, t < t3 ? t2 + 3.8 : stopA + 0.2, t < t3 ? t2 + 6.3 : stopB - 0.1, o.x, o.y); }
        show(this.lab[0], t, t2 + 0.5, t3 + 0.2); show(this.lab[1], t, t2 + 3.2, t3 + 0.2);
        if (inRace) {
          const span = 6.2, a = clamp((t - r0) / span, 0, 1), stopped = clamp(t - stopA, 0, stopB - stopA), b = clamp((t - r0 - stopped) / span, 0, 1);
          show(this.track, t, r0, t4 - 0.1, { dy: 0 }); show(this.goal, t, r0, t4 - 0.1, { dy: 0 });
          this.dotA.style.left = (360 + a * 1090) + 'px'; this.dotB.style.left = (360 + b * 1090) + 'px'; this.lose.style.left = (360 + b * 1090 + 140) + 'px';
          show(this.dotA, t, r0, t4 - 0.1, { dy: 0 }); show(this.dotB, t, r0, t4 - 0.1, { dy: 0 }); show(this.lose, t, stopA + 0.5, t4 - 0.1, { dx: -16, dy: 0 });
        }
        return 'studio';
      }
      if (t < t5) {                                   // the taunt reaches the one it is for
        const s = t - t4, cut = 3.7, hit = 1.0;
        const mz = lerp(1.6, -2.2, clamp(s / hit, 0, 1)), k = easeOut(prog(s, hit, hit + 0.9)), fz = lerp(-3.3, -7.0, k), fy = 0.29 * (1 - prog(k, 0.2, 0.55)) + 0.5 * 4 * k * (1 - k);
        const press = 1.9, taunt = win(s, press, 9, 0.25, 0.3), up = smooth(prog(s, cut + 0.2, cut + 1.1));
        A('me').wear(LOOK.me).place(0, 0.29, mz, Math.PI).pose({ t, walk: s < hit ? 1 : 0, phase: s * 12, emote: s < press ? 'shove' : 'taunt', w: s < press ? win(s, hit - 0.1, hit + 0.6, 0.1, 0.25) : taunt });
        A('foe', 'pawn', 'black').wear({ head: 'top' }).place(0, s < cut ? fy + 0.3 * k * (1 - up) : 0.3 * (1 - up), fz, lerp(0, Math.PI / 2, up)).tip(-1.45 * k * (1 - up)).pose({ t, seed: 3 });
        if (s < cut) {
          look(w, behind(0, 0.29, Math.min(mz, 0.4), Math.PI, { dist: 5.8, h: 2.6, lift: 0.4, side: 2.1 }));
          show(this.mine, t, t4 + 0.2, t4 + cut, { dx: -30, dy: 0 });
          show(this.prompt, t, t4 + 1.25, t4 + press + 0.35, { pop: true, s: 0.7, keep: `scale(${1 - 0.1 * win(s, press - 0.06, press + 0.2, 0.05, 0.1)})` });
          pin(w, this.bubMe, 0, 1.75, mz, t, t4 + press, t4 + cut);
        } else {
          w.view(-4.8, 2.3, -7.0, 2.0, 0.8, -7.0, 42);
          this.frame.style.visibility = 'visible'; this.frame.style.opacity = '1';
          show(this.theirs, t, t4 + cut, 1e9, { dx: -30, dy: 0 });
          show(this.edge, t, t4 + cut + 1.7, 1e9, { pop: true, s: 0.5, dx: 60, dy: 0, keep: `scale(${1 + 0.07 * Math.sin(s * 9)})` });
        }
        return 'hall';
      }
      // chess symbols, and the guards
      show(this.symTitle, t, t5 + 0.2);
      this.syms.forEach((e, i) => show(e, t, t5 + 0.9 + i * 0.75, 1e9, { pop: true, s: 0.7 }));
      show(this.faces, t, t5 + 4.4); show(this.stamp, t, t5 + 5.2);
      show(this.guards, t, c.at(6) + 0.3, 1e9, { pop: true, s: 0.8 });
    },
  };

  // ---------- 4 accessories ----------
  const LINE = [['pawn', -3.2], ['knight', -1.6], ['bishop', 0], ['rook', 1.6], ['queen', 3.2]];
  S.skin = {
    make(root) {
      this.wire = el('div', '', '<svg class="over">' + [0, 1, 2, 3].map(() => '<line stroke="#2b2522" stroke-width="5" stroke-linecap="round" stroke-dasharray="1 12"/>').join('') + '</svg>', root);
      this.slots = [['머리', '고깔모자'], ['얼굴', '안경'], ['등', '망토'], ['발자국', '별']].map(([a, b], i) => el('div', 'card', `<span class="disp h3">${a}</span> &nbsp;<span class="soft">${b}</span>`, root, { left: 1190, top: 220 + i * 150, width: 520 }));
      this.rule1 = el('div', 'card hot', '<span class="disp h3 wine">규칙 ①</span> &nbsp;기물의 실루엣을 가리지 않는다', root, { left: 56, top: 124 });
      this.rule1b = el('div', 'card', '머리 장식은 표지 위에 <b class="wine">작게</b> 얹는다 &nbsp;·&nbsp; 얼굴 · 등 · 발자국은 그대로', root, { left: 0, top: 800 });
      this.names = LINE.map(([k]) => tag(root, NAME[k], 'plain'));
      this.rings = LINE.map(() => el('div', 'ring', '', root, { width: 170, height: 170 }));
      this.sign = tag(root, '머리 모양 = 그 기물의 표지', 'wine');
      this.rule2 = el('div', 'card hot', '<span class="disp h3 wine">규칙 ②</span> &nbsp;팀 색은 바꿀 수 없다', root, { left: 56, top: 124 });
      this.noColor = el('div', 'chip', '<span class="strike">몸 색 바꾸기</span>', root, { left: 800, top: 420 });
      this.rule3 = el('div', 'card hot', '<span class="disp h3 wine">규칙 ③</span> &nbsp;판정은 그대로 · 충돌도 무게도 없다', root, { left: 56, top: 124 });
      this.eq = el('div', 'disp', '=', root, { left: 900, top: 380, width: 120, textAlign: 'center', fontSize: 170, color: 'var(--teal)', lineHeight: 1 });
      this.capTags = [tag(root, '꾸미기 없음', 'plain'), tag(root, '꾸미기 있음', 'plain')];
      this.fit = el('div', 'card hot', '<span class="disp h3 wine">아이템 1개</span> &nbsp;→ 모든 기물에 맞는다', root, { left: 56, top: 124 });
      this.fitNote = el('div', 'chip', '모두 폰의 뼈대를 쓴다 · 보정표 4슬롯 × 5기물', root, { left: 0, top: 800 });
      this.seenTitle = el('div', 'disp h2', '보이는 곳', root, { left: 0, top: 130, width: 1920, textAlign: 'center' });
      this.seen = [['🏛', '광장', '가장 오래 보이는 곳'], ['⏳', '로딩', '양 팀 라인업'], ['🏁', '경기', '상대 눈앞에서'], ['🏆', '결과', '이긴 팀의 무대']].map(([i, a, b], n) =>
        el('div', 'card', `<div style="font-size:96px;line-height:1.1">${i}</div><div class="disp h3">${a}</div><div class="soft small">${b}</div>`, root, { left: 180 + n * 400, top: 300, width: 340, height: 300, textAlign: 'center' }));
      this.arrows = [0, 1, 2].map(n => el('div', 'arrow', '›', root, { left: 532 + n * 400, top: 400, fontSize: 80 }));
      this.seenNote = el('div', 'h3', '남이 보는 자리가 있어야 꾸미기가 <b class="wine">팔립니다</b>', root, { left: 0, top: 680, width: 1920, textAlign: 'center' });
    },
    centre(e) { e.style.left = (960 - e.offsetWidth / 2) + 'px'; },
    lineup(t, wear, hatK, restK) {
      w.studio.P.floor.scale.setScalar(4.7); w.studio.P.rim.scale.setScalar(4.7);
      w.view(0, 1.7, 9.6, 0, 0.72, 0, 30);
      LINE.forEach(([kind, x], i) => {
        const a = B('l_' + kind, kind).wear(wear ? { head: 'party', face: 'glasses', back: 'cape' } : {}).place(x, 0, 0, kind === 'knight' ? -0.95 : -x * 0.06).pose({ t, seed: i });
        if (wear) { a.size('party', hatK(i)); a.size('glasses', restK(i)); a.size('cape', restK(i)); }
      });
    },
    draw(t, c) {
      const all = [this.wire, ...this.slots, this.rule1, this.rule1b, ...this.names, ...this.rings, this.sign, this.rule2, this.noColor, this.rule3, this.eq, ...this.capTags, this.fit, this.fitNote, this.seenTitle, ...this.seen, ...this.arrows, this.seenNote];
      for (const e of all) hide(e);
      const P = w.studio.P, t1 = c.at(1), t2 = c.at(2), t3 = c.at(3), t4 = c.at(4), t5 = c.at(5);
      if (t < t1) {                                   // four slots on one pawn
        const pops = [c.at(0) + 3.0, c.at(0) + 3.8, c.at(0) + 4.5, c.at(0) + 5.6];
        const where = u => { const f = 0.9 * u + 2.2; return [0.45 * Math.sin(f), 0.45 * Math.cos(f), f + Math.PI / 2]; }, [x, z, yaw] = where(t);
        P.floor.scale.setScalar(2.6); P.rim.scale.setScalar(2.6);
        w.view(1.25, 1.6, 5.4, 1.25, 0.6, 0, 30);
        const a = B('s1').wear({ head: 'party', face: 'glasses', back: 'cape' }).place(x, 0, z, yaw).pose({ t, walk: 0.7, phase: t * 8 });
        a.size('party', back(prog(t, pops[0], pops[0] + 0.4))); a.size('glasses', back(prog(t, pops[1], pops[1] + 0.4))); a.size('cape', back(prog(t, pops[2], pops[2] + 0.4)));
        if (t > pops[3]) P.stars.draw(t, u => where(u), 0.012, 0.24, 2.4, pops[3]);
        const fx = Math.sin(yaw), fz = Math.cos(yaw), b = where(t - 0.9);
        const pts = [w.project(x, 1.22, z), w.project(x + fx * 0.2, 0.86, z + fz * 0.2), w.project(x - fx * 0.3, 0.5, z - fz * 0.3), w.project(b[0], 0.02, b[1])];
        const ln = this.wire.querySelectorAll('line');
        this.wire.style.visibility = 'visible'; this.wire.style.opacity = '1'; this.wire.style.transform = '';
        this.slots.forEach((e, i) => {
          const op = show(e, t, pops[i], t1 - 0.1, { dx: 40, dy: 0 });
          ln[i].setAttribute('x1', 1186); ln[i].setAttribute('y1', 220 + i * 150 + 48); ln[i].setAttribute('x2', pts[i].x.toFixed(1)); ln[i].setAttribute('y2', pts[i].y.toFixed(1)); ln[i].setAttribute('opacity', (op * 0.75).toFixed(3));
        });
        return 'studio';
      }
      if (t < t3) {                                   // rule 1: the silhouette
        const hats = t2 + 4.6, rest = t2 + 6.6;
        this.lineup(t, t >= t2, i => back(prog(t, hats + i * 0.14, hats + i * 0.14 + 0.4)), i => back(prog(t, rest + i * 0.14, rest + i * 0.14 + 0.4)));
        show(this.rule1, t, t1 + 0.3, t3 - 0.1, { dx: -30, dy: 0 });
        LINE.forEach(([kind, x], i) => {
          pin(w, this.names[i], x, -0.1, 0.5, t, t1 + 0.5 + i * 0.12, t3 - 0.1, { keep: 'translate(-50%, 0)', dy: -10 });
          pin(w, this.rings[i], x, 0.1 + HEIGHT[kind] * (kind === 'pawn' ? 0.8 : 0.86), 0, t, t2 + 1.0 + i * 0.12, hats + 0.2, { keep: 'translate(-50%, -50%)', dy: 0, s: 1.6 });
        });
        pin(w, this.sign, 0, 2.0, 0, t, t2 + 1.6, hats + 0.2);
        this.centre(this.rule1b); show(this.rule1b, t, hats, t3 - 0.1);
        return 'studio';
      }
      if (t < t4) {                                   // rules 2 and 3: team colour, hit shape
        const third = t3 + 5.0;
        P.floor.scale.setScalar(2.9); P.rim.scale.setScalar(2.9);
        w.view(0, 1.4, 6.2, 0, 0.62, 0, 30);
        const bare = t >= third, off = 1 - prog(t, third, third + 0.3);
        const wp = B('c_w').wear(bare && off <= 0 ? {} : { head: 'party', face: 'glasses', back: 'cape' }).place(-1.15, 0, 0, 0.25).pose({ t, seed: 0 });
        if (bare) { wp.size('party', off); wp.size('glasses', off); wp.size('cape', off); }
        const bp = B('c_b', 'pawn', 'black').wear({ head: 'party', face: 'glasses', back: 'cape' }).place(1.15, 0, 0, -0.25).pose({ t, seed: 1 });
        wp.capsule(t > third + 0.5); bp.capsule(t > third + 0.5);
        show(this.rule2, t, t3 + 0.3, third - 0.1, { dx: -30, dy: 0 }); show(this.noColor, t, t3 + 1.6, third - 0.1, { pop: true, s: 0.7 });
        show(this.rule3, t, third, t4 - 0.1, { dx: -30, dy: 0 }); show(this.eq, t, third + 0.9, t4 - 0.1, { pop: true, s: 0.5 });
        pin(w, this.capTags[0], -1.15, -0.1, 0.5, t, third + 0.6, t4 - 0.1, { keep: 'translate(-50%, 0)', dy: -10 });
        pin(w, this.capTags[1], 1.15, -0.1, 0.5, t, third + 0.6, t4 - 0.1, { keep: 'translate(-50%, 0)', dy: -10 });
        return 'studio';
      }
      if (t < t5) {                                   // one item fits every piece
        const s = t - t4, allOn = 4.9;
        this.lineup(t, true, i => (s > allOn ? back(prog(s, allOn + i * 0.08, allOn + i * 0.08 + 0.35)) : win(s, 0.5 + i * 0.85, 0.5 + i * 0.85 + 0.95, 0.2, 0.2)), () => 1);
        show(this.fit, t, t4 + 0.3, t5 - 0.1, { dx: -30, dy: 0 });
        LINE.forEach(([kind, x], i) => pin(w, this.names[i], x, -0.1, 0.5, t, t4 + 0.2, t5 - 0.1, { keep: 'translate(-50%, 0)', dy: -10 }));
        this.centre(this.fitNote); show(this.fitNote, t, t4 + 2.6, t5 - 0.1);
        return 'studio';
      }
      show(this.seenTitle, t, t5 + 0.2);
      this.seen.forEach((e, i) => show(e, t, t5 + 0.9 + i * 0.6, 1e9, { pop: true, s: 0.8 }));
      this.arrows.forEach((e, i) => show(e, t, t5 + 1.3 + i * 0.6, 1e9, { dy: 0 }));
      show(this.seenNote, t, t5 + 3.6);
    },
  };

  // ---------- 5 selling ----------
  S.money = {
    make(root) {
      this.head = el('div', 'row', '<div class="chip grey" style="font-size:38px">게임 서버 없음</div><div class="arrow">→</div><div class="chip fill" style="font-size:52px">Steam 인벤토리 서비스</div><div class="soft" style="font-size:36px">서버 없이 조회 · 구매 · 교환 · 드롭</div>', root, { left: 120, top: 124 });
      const lane = (y, name, cls, boxes) => {
        const row = el('div', 'row', '', root, { left: 120, top: y, gap: '20px', fontSize: 42 }), parts = [el('div', 'chip ' + cls, name, row, { width: 170, fontSize: 46 })];
        boxes.forEach((b, i) => { if (i) parts.push(el('div', 'arrow', '→', row)); parts.push(el('div', 'card', b, row, { padding: '24px 32px', whiteSpace: 'nowrap' })); });
        return parts;
      };
      this.base = lane(268, '기본', 'fill', ['처음부터 있음', '감정표현 4 &nbsp;·&nbsp; 액세서리 2~3']);
      this.paid = lane(424, '유료', 'winefill', ['상점 진열대', '<b>Steam 결제창</b>', '내 옷장']);
      this.noBox = el('div', 'chip', '<span class="strike">뽑기</span> 없음 · 직접 구매', root, { left: 1330, top: 436, fontSize: 38 });
      this.free = lane(580, '무료', 'tealfill', ['⏱ 플레이 시간', '◆◆◆ 조각', '모아서 교환', '무료 아이템']);
      this.no = el('div', 'card', '<span class="disp h3 red">✕ 안 되는 것</span> &nbsp; <span class="strike">이기면 코인</span> &nbsp;<span class="soft">서버가 없어 경기 결과를 믿을 수 없습니다</span>', root, { left: 120, top: 744, borderColor: 'var(--red)', fontSize: 42, padding: '26px 36px' });
      this.veil = el('div', 'veil', '', root);
      this.need = el('div', 'row', '<div class="card" style="text-align:center;width:720px;padding:40px 30px"><div class="disp h2 wine">실제 판매</div><div style="font-size:50px;margin-top:12px">우리 게임의 App ID 필요</div><div class="soft" style="font-size:34px;margin-top:8px">지금은 개발용 480</div></div><div class="arrow" style="font-size:80px">→</div><div class="card hot" style="text-align:center;width:780px;padding:40px 30px"><div class="disp h2 teal">그 전에는</div><div style="font-size:50px;margin-top:12px">모의 인벤토리로</div><div class="soft" style="font-size:34px;margin-top:8px">옷장 · 상점 화면 · 장착 · 전달부터</div></div>', root, { left: 0, top: 300, width: 1920, justifyContent: 'center', gap: '40px' });
    },
    draw(t, c) {
      show(this.head, t, 0.5, 1e9, { dx: -30, dy: 0 });
      const lane = (parts, t0) => parts.forEach((e, i) => show(e, t, t0 + i * 0.22, 1e9, { dx: -24, dy: 0 }));
      lane(this.base, c.at(0) + 3.0); lane(this.paid, c.at(1) + 0.3); show(this.noBox, t, c.at(1) + 5.4, 1e9, { pop: true, s: 0.7 });
      lane(this.free, c.at(2) + 0.3); show(this.no, t, c.at(3) + 0.6, 1e9, { pop: true, s: 0.85 });
      const v = prog(t, c.at(4), c.at(4) + 0.5);
      this.veil.style.visibility = v > 0 ? 'visible' : 'hidden'; this.veil.style.opacity = (0.93 * v).toFixed(3);
      show(this.need, t, c.at(4) + 0.5);
    },
  };

  // ---------- 6 minigames ----------
  const MINI = [[2, 4], [3, 2], [1, 1], [0, 3], [2, 2], [4, 3], [3, 1], [1, 0], [0, 2]];
  const WIDE = cam(4.1, 7.2, -6.0, -1.8, 0.3, -13.6, 36), NEAR = cam(-4.2, 9.2, -6.4, HALL.pads.x + 1.2, 0.2, -13.2, 36), IN = path([[-1.5, -8.5], [-3.6, -9.6], [padAt(2, 4).x, padAt(2, 4).z]]);
  S.mini = {
    make(root) {
      this.list = ['걸어가면 바로 시작', '매칭되면 벌칙 없이 끊김', '한 판 20~60초', '혼자 되고, 파티면 더 재미', '경기와 같은 조작', '체스가 규칙'].map((s, i) =>
        el('div', 'card', `<div class="row"><span class="num">${i + 1}</span><span style="font-size:40px">${s}</span></div>`, root, { left: 1090, top: 150 + i * 122, width: 720, padding: '18px 28px' }));
      this.title = el('div', 'card hot', '<span class="disp h3 wine">나이트 점프</span><br><span class="soft">나이트가 가는 칸만 밟아 모든 칸을 켠다</span>', root, { left: 56, top: 124 });
      this.count = el('div', 'hud-card', '', root, { left: 56, top: 300, fontSize: 44 });
      this.legend = el('div', 'card', '<span style="color:#2a9d94">■</span> 갈 수 있는 칸 &nbsp; <span style="color:#d9a520">■</span> 밟은 칸', root, { left: 56, top: 400 });
      this.board = el('div', 'card', '<div class="disp h3">친구 순위</div><div class="soft small">Steam 리더보드 · 나이트 점프</div><div style="margin-top:16px;line-height:1.7;font-size:40px">🥇 민수 &nbsp; 31.2초<br>🥈 <b class="wine">나</b> &nbsp; 34.8초<br>🥉 지우 &nbsp; 41.0초</div>', root, { left: 1210, top: 200, width: 600 });
      this.noPrize = el('div', 'chip', '아이템 보상 없음 · 기록만', root, { left: 1210, top: 620 });
    },
    draw(t, c) {
      for (const e of [...this.list, this.title, this.count, this.legend, this.board, this.noPrize]) hide(e);
      const t3 = c.at(3), t4 = c.at(4), when = [c.at(0) + 2.4, c.at(0) + 4.6, c.at(1) + 0.3, c.at(1) + 2.6, c.at(2) + 0.3, c.at(2) + 3.6];
      this.list.forEach((e, i) => show(e, t, when[i], t3 - 0.1, { dx: 40, dy: 0 }));
      const u = clamp((t - 0.4) / 3.0, 0, 1), p = IN.at(u), start = t3 + 0.9, q = padHops(MINI, t, start, 0.85, 0.5), on = u >= 1;
      const x = t >= start ? q.x : p.x, y = t >= start ? q.y - 0.045 : (on ? padAt(2, 4).y - 0.045 : 0), z = t >= start ? q.z : p.z;
      if (on) padsState(w, MINI.slice(0, q.reached + 1), 0.5 + 0.5 * Math.sin(t * 7), !q.air);
      A('me').wear(LOOK.me).place(x, y, z, t >= start && q.air ? q.yaw : (on ? 2.4 : p.yaw)).pose({ t, walk: on ? 0 : 1, phase: t * 11, emote: 'cheer', w: q.air ? 0.5 : (t > t4 ? win(t, t4 + 0.5, c.dur, 0.3, 0.3) * 0 : 0) });
      const fu = clamp((t - c.at(1) - 2.2) / 2.2, 0, 1), fp = lerp(-9.8, padAt(4, 4).z, fu);
      if (t < t3 - 0.2) A('f1').wear(LOOK.f1).place(padAt(4, 4).x + (1 - fu) * 2, fu >= 1 ? padAt(4, 4).y - 0.045 : 0, fp, Math.PI).pose({ t, seed: 1, walk: fu > 0 && fu < 1 ? 1 : 0, phase: t * 11, emote: 'wave', w: fu >= 1 ? 1 : 0 });
      look(w, WIDE, NEAR, smooth(prog(t, t3 - 0.5, t3 + 0.8)));
      show(this.title, t, t3 + 0.3, 1e9, { dx: -30, dy: 0 });
      this.count.innerHTML = `<b style="color:#ffd66b">${q.reached + 1}</b> / 25 칸 &nbsp;·&nbsp; ${Math.max(0, t - start).toFixed(1)}초`;
      show(this.count, t, start, 1e9, { dx: -30, dy: 0 }); show(this.legend, t, t3 + 1.6, 1e9, { dx: -30, dy: 0 });
      show(this.board, t, t4 + 0.4, 1e9, { dx: 40, dy: 0 }); show(this.noPrize, t, t4 + 2.6, 1e9, { pop: true, s: 0.7 });
      return 'hall';
    },
  };

  // ---------- 7 friends and messages ----------
  S.friend = {
    make(root) {
      const dot = col => `<span style="display:inline-block;width:20px;height:20px;border-radius:50%;background:${col};margin-right:14px"></span>`;
      this.list = el('div', 'card', `<div class="disp h3">친구 <span class="teal" style="font-size:36px">2명 접속</span></div><div style="margin-top:14px;line-height:1.9">${dot('#3e9b4f')}민수 <span class="soft small">로비에서 대기 중</span> <span class="chip" style="font-size:26px;padding:4px 16px;float:right">초대</span><br>${dot('#c99a2e')}지우 <span class="soft small">폰 러쉬 경기 중</span><br>${dot('#b9ab97')}하늘 <span class="soft small">오프라인</span></div>`, root, { left: 150, top: 180, width: 640 });
      this.party = el('div', 'card', '<div class="disp h3">파티 채팅</div><div style="margin-top:10px;line-height:1.6">민수: 한 판 더?<br>지우: ㄱㄱ</div>', root, { left: 150, top: 590, width: 640 });
      this.have = [el('div', 'chip tealfill', '이미 있음', root, { left: 640, top: 150, fontSize: 30 }), el('div', 'chip tealfill', '이미 있음', root, { left: 640, top: 560, fontSize: 30 })];
      const say = (who, text, mine) => `<div style="display:flex;justify-content:${mine ? 'flex-end' : 'flex-start'};margin-top:14px"><div style="background:${mine ? '#9a2f3f' : '#efe5d4'};color:${mine ? '#fff8ea' : '#2b2522'};border-radius:22px;padding:12px 26px;max-width:480px">${mine ? '' : `<span class="small soft">${who}</span><br>`}${text}</div></div>`;
      this.dm = el('div', 'card hot', `<div class="disp h3">민수 <span class="soft" style="font-size:32px">1:1 대화</span></div>${say('민수', '오늘 폰 러쉬 ㄱ?', false)}${say('', 'ㄱㄱ 내 광장으로 와', true)}${say('민수', '가는 중 🏃', false)}`, root, { left: 1010, top: 180, width: 720 });
      this.fresh = el('div', 'chip winefill', '새로 만드는 것', root, { left: 1540, top: 150, fontSize: 30 });
      this.p2p = el('div', 'card', '<div class="row" style="justify-content:space-between"><div class="chip">내 PC</div><div class="teal" style="flex:1;text-align:center;border-top:7px dashed var(--teal);margin:0 16px;padding-top:6px">직접 주고받음</div><div class="chip">친구 PC</div></div><div class="soft small" style="margin-top:14px">게임을 켠 Steam 친구끼리 · 기록은 내 PC에 · <span class="strike">서버</span> 없음</div>', root, { left: 1010, top: 640, width: 720 });
      this.toast = el('div', 'hud-chip', '<span class="dot"></span>민수 님이 파티에 참가했어요', root, { left: 0, top: 150 });
    },
    draw(t, c) {
      const t2 = c.at(2);
      for (const e of [this.list, this.party, ...this.have, this.dm, this.fresh, this.p2p, this.toast]) hide(e);
      if (t < t2) {
        show(this.list, t, 0.5, t2 - 0.1, { dx: -30, dy: 0 }); show(this.party, t, 0.9, t2 - 0.1, { dx: -30, dy: 0 });
        this.have.forEach((e, i) => show(e, t, c.at(0) + 1.6 + i * 0.3, t2 - 0.1, { pop: true, s: 0.6 }));
        show(this.dm, t, c.at(0) + 4.6, t2 - 0.1, { dx: 30, dy: 0 }); show(this.fresh, t, c.at(0) + 5.2, t2 - 0.1, { pop: true, s: 0.6 });
        show(this.p2p, t, c.at(1) + 0.5, t2 - 0.1, { dy: 30 });
        return;
      }
      const s = t - t2, fz = Math.min(-20.6, -27 + Math.max(0, s - 0.9) * 2.4), there = fz >= -20.6;
      noInstall();
      w.view(5.8, 3.2, -12.8, -0.3, 0.5, -21.5, 38);
      w.gate(smooth(prog(s, 0.2, 1.4)));
      A('f1').wear(LOOK.f1).place(0, 0, fz, 0).pose({ t, walk: there ? 0 : 1, phase: s * 9, emote: 'wave', w: there ? 1 : 0 });
      A('me').wear(LOOK.me).place(2.5, 0, -18.4, Math.PI + 0.35).pose({ t, emote: 'cheer', w: win(s, 1.6, 9, 0.4, 0.4) });
      this.toast.style.left = (960 - this.toast.offsetWidth / 2) + 'px'; show(this.toast, t, t2 + 1.4, 1e9, { pop: true, s: 0.7, dy: -20 });
      return 'hall';
    },
  };

  // ---------- the order ----------
  S.order = {
    make(root) {
      const steps = [['혼자 걷는 광장', '홀을 걷다가<br>매칭되면 대문으로', '변경 없음', 'grey'], ['액세서리 · 옷장', '모자를 쓰고<br>경기에 들어간다', '멤버 데이터 한 줄', 'grey'], ['감정표현', '경기에서<br>놀린다', '프로토콜 변경', 'winefill'], ['파티 광장', '친구와<br>같이 걷는다', '프로토콜 변경', 'winefill'],
        ['미니게임 2종', '기다리며<br>한 판', '4단계 흐름 사용', 'grey'], ['메신저', '1:1 대화', '새 P2P 채널', 'grey'], ['실제 상점', '실제 구매', 'App ID 필요', 'fill']];
      this.cards = steps.map(([a, b], i) => el('div', 'card', `<div class="num" style="margin:0 auto 14px">${i + 1}</div><div class="disp" style="font-size:38px;line-height:1.15">${a}</div><div class="soft" style="font-size:28px;margin-top:14px;font-weight:500">${b}</div>`, root, { left: 100 + i * 248, top: 190, width: 232, height: 330, textAlign: 'center', padding: '22px 12px' }));
      this.net = steps.map(([, , n, cls], i) => el('div', 'chip ' + cls, n, root, { left: 100 + i * 248, top: 540, width: 232, fontSize: 25, padding: '8px 4px' }));
      this.netLabel = el('div', 'soft small', '네트워크', root, { left: 100, top: 600 });
      this.same = el('div', 'card hot', '<span class="disp h3 wine">3 · 4단계</span> &nbsp;프로토콜 번호가 바뀐다 → 그때는 모두 같은 빌드', root, { left: 0, top: 690 });
      this.cost = el('div', 'card hot', '<div class="disp h3">진짜 비용은 코드가 아니라 <span class="wine">모델과 동작</span></div><div style="margin-top:8px;font-size:40px">첫 묶음은 작게: 액세서리 8개 · 감정표현 6개</div>', root, { left: 0, top: 670, textAlign: 'center' });
    },
    draw(t, c) {
      const from = [c.at(0) + 2.6, c.at(1) + 0.4, c.at(1) + 2.6, c.at(2) + 0.8, c.at(2) + 3.0, c.at(2) + 4.2, c.at(3) + 0.4];
      const line = [0, 1, 1, 2, 2, 2, 3], now = c.lines.findLastIndex(l => t >= l.t0 - 0.1), pulse = win(t, c.at(4) + 0.3, c.at(5) - 0.2, 0.3, 0.3);
      this.cards.forEach((e, i) => {
        const hot = (now === line[i]) || (now === 4 && (i === 2 || i === 3));
        e.className = 'card' + (hot ? ' hot' : '');
        const op = show(e, t, from[i], 1e9, { pop: true, s: 0.8 }); e.style.opacity = (op * (hot || now < 0 ? 1 : 0.55)).toFixed(3);
        show(this.net[i], t, from[i] + 0.3, 1e9, { dy: -10, keep: `scale(${1 + ((i === 2 || i === 3) ? 0.1 * pulse * (0.6 + 0.4 * Math.sin(t * 8)) : 0)})` });
      });
      show(this.netLabel, t, from[0] + 0.5);
      this.same.style.left = (960 - this.same.offsetWidth / 2) + 'px'; show(this.same, t, c.at(4) + 0.5, c.at(5) - 0.1, { pop: true, s: 0.85 });
      this.cost.style.left = (960 - this.cost.offsetWidth / 2) + 'px'; show(this.cost, t, c.at(5) + 0.5, 1e9, { pop: true, s: 0.85 });
    },
  };

  // ---------- what to decide together ----------
  S.decide = {
    make(root) {
      const Q = [['광장에 누구까지 들일까', ['<span class="chip tealfill">파티만 먼저</span>', '<span class="chip">매칭된 방 사람</span>', '<span class="chip">공개</span>']],
        ['본편은 유료? 무료? 가격대는', ['<span class="chip">팀에서 결정</span>']],
        ['첫 묶음에 무엇을 넣을까', ['<span class="chip tealfill">액세서리 8</span>', '<span class="chip tealfill">감정표현 6</span>']],
        ['첫 미니게임은', ['<span class="chip tealfill">나이트 점프</span>', '<span class="chip tealfill">판 밖으로</span>', '<span class="chip">메이트 인 원</span>', '<span class="chip">체스 시계</span>']]];
      this.cards = Q.map(([q], i) => el('div', 'card', `<div class="row"><span class="num">${i + 1}</span><span class="disp h3">${q}</span></div>`, root, { left: 110 + (i % 2) * 870, top: 170 + Math.floor(i / 2) * 330, width: 830, height: 290, padding: '30px 36px' }));
      this.opts = Q.map(([, o], i) => el('div', 'row', o.join(''), this.cards[i], { left: 36, top: 130, width: 760, flexWrap: 'wrap', gap: '14px', fontSize: 32, position: 'absolute' }));
      this.legend = el('div', 'soft small', '<span class="teal">■</span> 초록 = 기획서의 추천', root, { left: 1480, top: 850 });
      this.veil = el('div', 'veil', '', root);
      this.end = el('div', '', '<div class="disp h1">의견 주세요</div><div class="soft" style="font-size:44px;margin-top:26px">기획서 &nbsp;Docs/Plaza/DESIGN.md</div>', root, { left: 0, top: 340, width: 1920, textAlign: 'center' });
    },
    draw(t, c) {
      const when = [c.at(1) + 0.2, c.at(1) + 2.7, c.at(1) + 5.3, c.at(1) + 7.6];
      this.cards.forEach((e, i) => show(e, t, c.at(0) + 0.5 + i * 0.3, 1e9, { pop: true, s: 0.8 }));
      this.opts.forEach((e, i) => show(e, t, when[i], 1e9, { dy: 16 }));
      show(this.legend, t, when[0] + 0.4);
      const v = prog(t, c.at(2), c.at(2) + 0.5);
      this.veil.style.visibility = v > 0 ? 'visible' : 'hidden'; this.veil.style.opacity = v.toFixed(3);
      show(this.end, t, c.at(2) + 0.5, 1e9, { dy: 40 });
    },
  };
  return S;
}
