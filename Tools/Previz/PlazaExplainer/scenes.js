// Plaza explainer - the scenes, first half: title, the lobby today, the big picture, the plaza, matching.
// Each scene has make(root) (its DOM, once) and draw(t, c) (everything at time t; c.at(i) is when line i starts).
// draw returns which 3D set is behind it: 'hall', 'studio' or nothing.
import { el, show, hide, prog, smooth, easeOut, back, lerp, clamp, win, mmss } from './kit.js';
import { HALL, padAt } from './world.js';
import { pin, tag, pointer, click, hud, hudPlaza, cam, behind, look, path, hop, LOOK, padsState, padHops } from './ui.js';
import { build2 } from './scenes2.js';

export function build(world) {
  const S = {}, w = world;
  const A = (id, kind, team) => w.actor('hall', id, kind, team), B = (id, kind, team) => w.actor('studio', id, kind, team);
  const noInstall = () => { for (const k of ['wardrobe', 'shop', 'pads', 'ring']) w.rise(k, 0); };

  // ---------- title ----------
  S.title = {
    make(root) {
      this.stamp = el('div', 'chip winefill', '제안 · 아직 정해진 것은 없습니다', root, { left: 140, top: 150, fontSize: 36 });
      this.k = el('div', 'wine', '체스파이트 · 추가 콘텐츠 기획 v0.1', root, { left: 140, top: 262, fontSize: 40, fontWeight: 700 });
      this.h = el('div', 'disp h1', '광장과 꾸미기', root, { left: 132, top: 318 });
      this.sub = el('div', 'soft', '매칭을 기다리는 시간을<br>놀고, 꾸미고, 이야기하는 시간으로', root, { left: 140, top: 520, fontSize: 46 });
      const box = el('div', '', '', root, { left: 140, top: 700, width: 1020, display: 'flex', flexWrap: 'wrap', gap: '16px' });
      this.chips = ['매칭 중 로비', '3D 광장', '친구 · 메신저', '감정표현', '스킨', '수익', '미니게임'].map(s => el('div', 'chip', s, box));
    },
    draw(t, c) {
      show(this.k, t, 0.3); show(this.h, t, 0.45, 1e9, { dy: 40 }); show(this.sub, t, 0.9);
      this.chips.forEach((e, i) => show(e, t, 1.5 + i * 0.16, 1e9, { pop: true, s: 0.7 }));
      show(this.stamp, t, c.at(1) + 0.2, 1e9, { pop: true, s: 1.5, dy: 0, keep: 'rotate(-3deg)' });
      const P = w.studio.P; P.floor.scale.setScalar(2.5); P.rim.scale.setScalar(2.5);
      w.view(-1.4, 1.2, 5.6, -1.4, 0.72, 0, 30);
      const cheer = win(t, c.at(1) + 0.4, c.dur, 0.4, 0.4);
      B('hero').wear(LOOK.me).place(0, 0, 0, -0.25).pose({ t, emote: cheer > 0 ? 'cheer' : 'wave', w: cheer > 0 ? cheer : win(t, 0.9, c.at(1) + 0.2, 0.4, 0.4) });
      B('rival', 'pawn', 'black').wear({ head: 'top', face: 'mustache' }).place(1.25, 0, -0.9, -0.6).pose({ t, seed: 2 });
      return 'studio';
    },
  };

  // ---------- the lobby today ----------
  const SPOTS = [[-1.9, -0.9], [0, -0.9], [1.9, -0.9], [-0.95, 0.9]];
  S.now = {
    make(root) {
      this.hud = hud(root); this.ptr = pointer(root);
      this.search = el('div', 'hud-card', '', root, { right: 56, bottom: 410, fontSize: 44 });
      this.stand = tag(root, '서 있기만 합니다');
      this.nothing = el('div', 'card', '매칭 중에 할 수 있는 것<br><span class="disp h2 red">없음</span>', root, { left: 110, top: 400 });
      this.veil = el('div', 'veil', '', root);
      const row = el('div', 'row', '', root, { left: 0, top: 360, width: 1920, justifyContent: 'center', gap: '70px' });
      this.words = ['놀고', '꾸미고', '이야기하는'].map((s, i) => el('div', 'disp', s, row, { fontSize: 150, color: ['var(--teal)', 'var(--wine)', 'var(--brass)'][i] }));
      this.tail = el('div', 'disp h2', '시간으로', root, { left: 0, top: 570, width: 1920, textAlign: 'center' });
    },
    draw(t, c) {
      noInstall();
      w.view(0.4, 2.7, -8.6, 0.4, 1.3, 2, 36);
      SPOTS.forEach((p, i) => A('n' + i).wear({}).place(p[0], 0.29, p[1], Math.PI).pose({ t, seed: i * 1.7, emote: 'stand', w: 1 }));
      const h = this.hud, press = c.at(1) + 1.2, end = c.at(2) + 0.3;
      for (const e of [h.profile, h.mode, h.chat, h.btn]) show(e, t, 0.4, end, { dy: 12 });
      hide(h.chip);
      h.btn.textContent = t < press ? '게임 시작' : '취소'; h.btn.className = 'hud-btn' + (t < press ? '' : ' off');
      click(this.ptr, t, c.at(1) + 0.3, press, press + 0.7, 1380, 600, 1716, 862);
      this.search.innerHTML = `<span class="dot"></span>상대를 찾는 중 &nbsp;<b style="color:#ffd66b">${mmss((t - press) * 7)}</b>`;
      show(this.search, t, press + 0.1, end, { dy: 14 });
      pin(w, this.stand, 0, 1.75, -0.9, t, c.at(0) + 2.6, c.at(1) + 0.2);
      show(this.nothing, t, press + 1.3, end, { dx: -30, dy: 0 });
      const v = prog(t, c.at(2), c.at(2) + 0.5);
      this.veil.style.opacity = (0.9 * v).toFixed(3); this.veil.style.visibility = v > 0 ? 'visible' : 'hidden';
      this.words.forEach((e, i) => show(e, t, c.at(2) + 2.0 + i * 0.75, 1e9, { pop: true, s: 0.6, dy: 30 }));
      show(this.tail, t, c.at(2) + 4.4);
      return 'hall';
    },
  };

  // ---------- the big picture ----------
  S.big = {
    make(root) {
      const items = ['매칭 중 로비', '3D 광장', '친구 · 메신저', '감정표현', '스킨', '수익', '미니게임'];
      this.A = [[235, 330], [605, 330], [975, 330], [1345, 330], [420, 450], [790, 450], [1160, 450]];   // seven in a grid
      this.B = [[596, 682], [166, 682], [166, 810], [1026, 682], [1026, 746], [836, 352], [166, 746]];   // gathered under the loop
      this.svg = el('div', '', `<svg class="over"><defs><marker id="ah" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M0 0 L10 5 L0 10 z" fill="#6b5f57"/></marker></defs>
        <g fill="none" stroke="#6b5f57" stroke-width="7" stroke-linecap="round" marker-end="url(#ah)">
        <path d="M468 565 H548"/><path d="M898 565 H978"/><path d="M1328 565 H1408"/>
        <path d="M1585 462 V265 H1160" stroke-dasharray="2 16"/><path d="M768 265 H295 V458" stroke-dasharray="2 16"/></g></svg>`, root);
      const node = (x, y, wd, title, sub) => el('div', 'card', `<div class="disp h3">${title}</div><div class="soft small">${sub}</div>`, root, { left: x, top: y, width: wd, height: 190, textAlign: 'center', paddingTop: 34 });
      this.nodes = [node(130, 470, 330, '광장', '머무는 곳'), node(560, 470, 330, '매칭', '기다림이 놀이'), node(990, 470, 330, '경기', '보여 주는 무대'), node(1420, 470, 330, '결과', '자랑')];
      this.shop = el('div', 'card', '<div class="disp h3">옷장 · 상점</div><div class="soft small">보여 줄 것을 얻는 곳</div>', root, { left: 775, top: 190, width: 378, height: 150, textAlign: 'center', paddingTop: 20 });
      this.chips = items.map((s, i) => el('div', 'chip', `<span class="wine">${i + 1}</span>&nbsp; ${s}`, root, { width: 330, transformOrigin: '0 0' }));
      this.title = el('div', 'disp h2', '일곱 가지', root, { left: 0, top: 170, width: 1920, textAlign: 'center' });
    },
    draw(t, c) {
      const m = smooth(prog(t, c.at(1) + 0.3, c.at(1) + 1.6)), focus = prog(t, c.at(3) + 0.2, c.at(3) + 0.8);
      show(this.title, t, 0.4, c.at(1) + 0.4);
      this.chips.forEach((e, i) => {
        e.style.left = lerp(this.A[i][0], this.B[i][0], m) + 'px'; e.style.top = lerp(this.A[i][1], this.B[i][1], m) + 'px';
        const op = show(e, t, c.at(0) + 0.3 + i * 0.2, 1e9, { pop: true, s: 0.7, dy: 20, keep: `scale(${lerp(1, 0.78, m)})` });
        e.style.opacity = (op * (1 - 0.6 * focus * (i === 1 ? 0 : 1))).toFixed(3);
      });
      show(this.svg, t, c.at(1) + 1.3, 1e9, { dy: 0 }); this.svg.style.opacity = (Number(this.svg.style.opacity) * (1 - 0.6 * focus)).toFixed(3);
      this.nodes.forEach((e, i) => {
        const op = show(e, t, c.at(1) + 0.5 + i * 0.25, 1e9, { pop: true, s: 0.8, keep: i === 0 ? `scale(${1 + 0.07 * focus})` : '' });
        e.style.opacity = (op * (i === 0 ? 1 : 1 - 0.6 * focus)).toFixed(3);
        e.className = 'card' + (i === 0 && focus > 0.3 ? ' hot' : '');
      });
      const op = show(this.shop, t, c.at(2) + 2.6, 1e9, { pop: true, s: 0.8 }); this.shop.style.opacity = (op * (1 - 0.6 * focus)).toFixed(3);
    },
  };

  // ---------- 1 the plaza ----------
  const RUN = path([[-1.0, -5.5], [-2.6, -9], [-1.0, -13], [1.8, -16], [2.6, -20.5]]);
  S.plaza = {
    make(root) {
      this.head = el('div', 'card', '<span class="disp h3">왕국 홀</span><br><span class="soft">지금 로비의 배경 그대로 · 새 맵 없음</span>', root, { left: 56, top: 120 });
      this.tags = { stair: tag(root, '큰 계단'), gate: tag(root, '대문'), board: tag(root, '체스판 · 파티가 모이는 자리'), ward: tag(root, '옷장', 'wine'), shop: tag(root, '상점', 'wine'), mg1: tag(root, '미니게임', 'teal'), mg2: tag(root, '미니게임', 'teal') };
      this.same = el('div', 'chip fill', '경기와 같은 캐릭터 · 같은 조작', root, { left: 56, top: 124 });
      this.keys = el('div', 'card', '', root, { left: 56, bottom: 172, whiteSpace: 'nowrap' });
      this.kWalk = el('span', '', '<span class="key">W</span><span class="key">A</span><span class="key">S</span><span class="key">D</span> 달리기 &nbsp;&nbsp;', this.keys);
      this.kJump = el('span', '', '<span class="key">Space</span> 점프 &nbsp;&nbsp;', this.keys);
      this.kPush = el('span', '', '<span class="key">클릭</span> 잡기 · 밀기', this.keys);
      this.names = [tag(root, '나', 'wine'), tag(root, '친구'), tag(root, '친구')];
      this.party = el('div', 'card', '<span class="disp h2">나 + 파티</span><br><span class="h3">최대 6명</span>', root, { right: 90, top: 210, width: 560 });
      this.later = el('div', 'card', '<span class="soft">모르는 사람이 섞이는</span><br><span class="disp h3 strike">공개 광장</span><br><span class="soft">서버가 없어 뒤로 미룸</span>', root, { right: 90, top: 500, width: 560 });
    },
    draw(t, c) {
      const T = this.tags, t3 = c.at(3), t4 = c.at(4);
      for (const e of [...Object.values(T), ...this.names, this.head, this.same, this.keys, this.party, this.later]) hide(e);
      if (t < t3) {                                   // looking down into the hall
        const a = lerp(0.74, 0.44, smooth(prog(t, 0, t3)));
        w.view(37 * Math.sin(a), 27, -4.5 - 37 * Math.cos(a), 0, 0, -5.5, 38);
        const r = [['wardrobe', 0.9], ['shop', 1.7], ['pads', 2.5], ['ring', 2.9]];
        for (const [name, dt] of r) w.rise(name, back(prog(t, c.at(2) + dt, c.at(2) + dt + 0.55)));
        A('me').wear(LOOK.me).place(-1.2, 0.29, -0.6, Math.PI + 0.3).pose({ t, seed: 0 });
        A('f1').wear(LOOK.f1).place(0.9, 0.29, -0.9, Math.PI - 0.4).pose({ t, seed: 1 });
        A('f2').wear(LOOK.f2).place(0.2, 0.29, 1.0, Math.PI).pose({ t, seed: 2 });
        show(this.head, t, 0.5, c.at(2) - 0.1, { dx: -30, dy: 0 });
        pin(w, T.stair, 0, 4.6, 9, t, c.at(1) + 0.5, t3 - 0.1); pin(w, T.gate, 0, 6.3, HALL.gateZ, t, c.at(1) + 2.6, t3 - 0.1); pin(w, T.board, 0, 1.4, 0, t, c.at(1) + 4.6, t3 - 0.1);
        pin(w, T.ward, HALL.wardrobe.x, 4.3, HALL.wardrobe.z, t, c.at(2) + 1.2, t3 - 0.1); pin(w, T.shop, HALL.shop.x, 3.9, HALL.shop.z, t, c.at(2) + 2.0, t3 - 0.1);
        pin(w, T.mg1, HALL.pads.x, 0.9, HALL.pads.z, t, c.at(2) + 2.8, t3 - 0.1); pin(w, T.mg2, HALL.ring.x, 1.3, HALL.ring.z, t, c.at(2) + 3.2, t3 - 0.1);
      } else if (t < t4) {                            // third person: run, jump, shove
        // the words come at about 4.4 (달리고), 5.1 (뛰고), 5.7 (잡고) and 6.3 s (밉니다); the friend is back up by 8.4
        const s = t - t3, u = clamp(s / 5.9, 0, 1), p = RUN.at(u), moving = u < 1 ? 1 : 0, jump = hop(s, 4.85, 5.5, 0.9);
        const push = win(s, 6.0, 7.0, 0.12, 0.3);
        A('me').wear(LOOK.me).place(p.x, jump, p.z, p.yaw).pose({ t, walk: moving * (jump > 0 ? 0.3 : 1), phase: s * 11, emote: 'shove', w: push });
        const end = RUN.at(1), fx = Math.sin(end.yaw), fz = Math.cos(end.yaw), k = easeOut(prog(s, 6.15, 7.0)), up = smooth(prog(s, 7.6, 8.3));
        A('f1').wear(LOOK.f1).place(end.x + fx * (1.0 + 2.4 * k), 0.3 * k * (1 - up), end.z + fz * (1.0 + 2.4 * k), end.yaw + Math.PI).tip(-1.45 * k * (1 - up)).pose({ t, seed: 1 });
        A('f2').wear(LOOK.f2).place(-5.0, 0, -11.5, 1.2).pose({ t, seed: 2, emote: 'wave', w: win(s, 1.6, 4.6, 0.4, 0.4) });
        look(w, behind(p.x, 0, p.z, p.yaw, { dist: 6.2, h: 2.4, lift: 0.45 }));
        show(this.same, t, t3 + 0.3, t4 - 0.1, { dx: -30, dy: 0 }); show(this.keys, t, t3 + 0.5, t4 - 0.1, { dy: 20 });
        const lit = (e, on) => { for (const k2 of e.querySelectorAll('.key')) k2.className = 'key' + (on ? ' on' : ''); };
        lit(this.kWalk, moving && jump <= 0); lit(this.kJump, jump > 0); lit(this.kPush, push > 0.2);
      } else {                                        // who is here: me and my party
        const s = t - t4;
        w.view(6.6 + s * 0.08, 4.9, -14.2, -2.9, -0.5, -6.4, 34);
        const who = [['me', -1.5, -5.2, 'cheer'], ['f1', 0.6, -5.6, 'wave'], ['f2', -0.3, -6.9, 'taunt']];
        who.forEach(([id, x, z, em], i) => {
          A(id).wear(LOOK[id]).place(x, 0, z, 0.9 + i * 0.5).pose({ t, seed: i, emote: em, w: win(s, 0.6 + i * 0.9, 3.4 + i * 0.9, 0.3, 0.4) });
          pin(w, this.names[i], x, 1.45, z, t, t4 + 0.3 + i * 0.2);
        });
        show(this.party, t, t4 + 0.5, 1e9, { dx: 30, dy: 0 }); show(this.later, t, t4 + 4.6, 1e9, { dx: 30, dy: 0 });
      }
      return 'hall';
    },
  };

  // ---------- 2 matching ----------
  const TO_PADS = path([[-0.5, -5], [-2.6, -7.2], [-4.6, -9.2], [padAt(2, 4).x, padAt(2, 4).z]]);
  const HOPS = [[2, 4], [3, 2], [1, 1], [0, 3], [2, 2], [4, 3], [3, 1]];
  const PADCAM = cam(1.0, 6.6, -4.4, HALL.pads.x, 0.3, HALL.pads.z - 0.2, 36);
  S.match = {
    make(root) {
      this.hud = hud(root); this.ptr = pointer(root);
      this.glow = el('div', 'glow', '', root);
      this.banner = el('div', 'card hot', '<span class="disp h2">상대를 찾았습니다</span>', root, { left: 400, top: 132, width: 900, textAlign: 'center' });
      this.digit = el('div', 'disp', '', root, { left: 1330, top: 112, width: 190, height: 190, borderRadius: '50%', background: 'var(--wine)', color: '#fff8ea', fontSize: 130, lineHeight: '190px', textAlign: 'center', boxShadow: '0 10px 26px rgba(154,47,63,.35)' });
      this.closed = tag(root, '미니게임 종료 · 벌칙 없음', 'teal');
      this.keeps = el('div', 'chip fill', '조작이 끊기지 않습니다', root, { left: 56, top: 124 });
      this.exists = el('div', 'chip fill', '이 연출은 지금 로비에 이미 있습니다', root, { left: 56, top: 124 });
      this.veil = el('div', 'veil', '', root);
      this.free = el('div', 'card', '<div class="disp h3 teal">매칭 중에도 자유</div><div style="margin-top:18px;line-height:1.75;font-size:42px">✓ 걷기 &nbsp; ✓ 미니게임<br>✓ 옷장 · 상점 구경과 입혀 보기<br>✓ 메신저</div>', root, { left: 130, top: 230, width: 800, padding: '36px 44px' });
      this.lock = el('div', 'card hot', '<div class="disp h3 wine">잠그는 것은 하나</div><div style="margin-top:18px;font-size:70px;font-weight:700">🔒 실제 결제</div><div class="soft" style="margin-top:18px;font-size:34px">결제하다 경기로 끌려가지 않게.<br>검색을 취소하면 바로 풀립니다.</div>', root, { left: 990, top: 230, width: 800, padding: '36px 44px' });
    },
    draw(t, c) {
      const h = this.hud, t1 = c.at(1), t2 = c.at(2), t3 = c.at(3), t4 = c.at(4), press = c.at(0) + 1.5;
      for (const e of [...h.all, this.glow, this.banner, this.digit, this.closed, this.keeps, this.exists, this.veil, this.free, this.lock]) hide(e);
      hide(this.ptr.arrow); hide(this.ptr.ring);
      if (t < t3) {
        const found = t >= t2;
        hudPlaza(h, t, 0.4, press, { speed: 5, until: t3 - 0.1, text: found ? '<span class="dot"></span>상대를 찾았습니다' : null });
        click(this.ptr, t, c.at(0) + 0.5, press, press + 0.6, 1420, 640, 1716, 862);
        show(this.keeps, t, press + 0.6, t1 + 2.2, { dx: -30, dy: 0 });
        const u = clamp((t - 0.3) * 1.75 / TO_PADS.len, 0, 1), p = TO_PADS.at(u);
        let x = p.x, y = u >= 1 ? padAt(2, 4).y - 0.045 : 0, z = p.z, yaw = p.yaw, walk = u < 1 ? 1 : 0, air = 0, reached = 0;
        const hopsFrom = t1 + 0.6, kk = Math.floor((t2 - hopsFrom) / 0.9), hopsUntil = (t2 - hopsFrom - kk * 0.9) < 0.55 ? hopsFrom + kk * 0.9 + 0.56 : t2;   // a hop under way when the match is found still lands
        if (t >= hopsFrom) { const q = padHops(HOPS, Math.min(t, Math.max(hopsUntil, hopsFrom)), hopsFrom); x = q.x; y = q.y - 0.045; z = q.z; yaw = q.air ? q.yaw : yaw; air = q.air; reached = q.reached; }
        const visited = HOPS.slice(0, reached + 1), off = found ? prog(t, t2 + 1.3, t2 + 1.7) : 0;
        if (u >= 1 && off < 0.5) padsState(w, visited, 0.5 + 0.5 * Math.sin(t * 7), !air && !found);
        A('me').wear(LOOK.me).place(x, y, z, yaw).pose({ t, walk, phase: t * 10, emote: 'cheer', w: air ? 0.5 : 0 });
        A('f1').wear(LOOK.f1).place(-1.0, 0.29, -1.0, 2.6).pose({ t, seed: 1 });
        A('f2').wear(LOOK.f2).place(1.2, 0.29, 0.4, 3.4).pose({ t, seed: 2 });
        look(w, behind(p.x, 0, p.z, p.yaw, { dist: 6.0, h: 2.9 }), PADCAM, smooth(prog(t, t1 - 0.9, t1 + 0.5)));
        if (found) {
          const s = t - t2;
          this.glow.style.visibility = 'visible'; this.glow.style.opacity = (0.55 + 0.45 * Math.sin(s * 9)) * prog(s, 0.1, 0.5) + '';
          show(this.banner, t, t2 + 0.25, t3 - 0.1, { pop: true, s: 0.7, dy: -30 });
          const n = Math.floor((s - 1.0) / 0.95), f = (s - 1.0) / 0.95 - n;
          if (s >= 1.0 && n < 3) { this.digit.textContent = String(3 - n); this.digit.style.visibility = 'visible'; this.digit.style.opacity = '1'; this.digit.style.transform = `scale(${lerp(1.35, 1, easeOut(prog(f, 0, 0.3)))})`; }
          pin(w, this.closed, HALL.pads.x, 1.5, HALL.pads.z, t, t2 + 1.5, t3 - 0.1);
        }
      } else if (t < t4) {                            // the party runs into the gate
        const s = t - t3;
        w.view(8.5, 5.4, -6.5, 0, 1.2, -21, 40);
        w.gate(smooth(prog(s, 0.1, 1.5)));
        [['me', 0, 0.2], ['f1', -1.0, 0.55], ['f2', 1.0, 0.9]].forEach(([id, x, t0], i) => {
          const z = -9 - Math.max(0, s - t0) * 4.4;
          if (z > -26.4) A(id).wear(LOOK[id]).place(x, 0, z, Math.PI).pose({ t, walk: s > t0 ? 1 : 0, phase: s * 13 + i });
        });
        show(this.exists, t, t3 + 3.6, t4 - 0.1, { dx: -30, dy: 0 });
      } else {                                        // what is locked while matching
        w.view(8.5, 5.4, -6.5, 0, 1.2, -21, 40); w.gate(1);
        this.veil.style.visibility = 'visible'; this.veil.style.opacity = (0.92 * prog(t, t4, t4 + 0.4)).toFixed(3);
        show(this.free, t, t4 + 0.4, 1e9, { dx: -30, dy: 0 }); show(this.lock, t, t4 + 1.6, 1e9, { dx: 30, dy: 0 });
      }
      return 'hall';
    },
  };

  Object.assign(S, build2(world, { A, B, noInstall }));
  return S;
}
