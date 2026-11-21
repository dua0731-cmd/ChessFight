# 폰 러시 엔딩 — 30초 소환 연출 프리비즈

기획: [`Docs/PawnRush/PAWN_RUSH_ENDING_SUMMON_STAGING_v0.1.md`](../../../Docs/PawnRush/PAWN_RUSH_ENDING_SUMMON_STAGING_v0.1.md) §3 · §4. **실제 게임 화면이 아니다** — 소리는 자막(♪)으로만, 무대는 국왕 알현실 B 대성당(회색 상자의 숫자).

| 클립 | 내용 | 누구의 화면 |
|---|---|---|
| `call` | 공통 '부름': 1등이 출구를 지나면 띠 "왕의 부름까지 30", 홀이 깨어남, 빨간 3 · 2 · 1 | 백 퀸(1등) → 백 룩(늦은 사람) |
| `hand` | ① 왕의 손 — 손이 집어 열린 지붕으로 내려와 내 칸에 '탁' | 백 룩, 중간에 백 퀸 |
| `procession` | ② 알현 행렬 — 큰 문이 열리고 융단을 따라 들어감 | 백 룩 |
| `setup` | ③ 판이 차려진다 — 천장 아래에서 폰 → 킹 순서로 '쿵' | 흑 폰 |
| `crown` | ④ 결전의 종 — 거대한 왕관이 발판에 '쾅' | 백 룩 |
| `king` | ⑤ 킹이 부른다(사용자 안, `king.js`) — 킹이 지팡이로 '탁!', 눈 빛 · 오로라 · 빛기둥, 모두 빨려 온다 | 백 퀸(먼저 옴) → 백 룩(코스에서 빨려 옴) |

모든 화면은 각자 자기 기물 뒤 3인칭이다(카메라를 빼앗아 위에서 보는 판으로 넘기지 않는다).

## 녹화

```
node record.mjs --clips call,hand,procession,setup,crown --out out/ending-summon.mp4
node record.mjs --sheets call,hand        # 클립마다 3x3 컷 (out/sheets)
```

헤드리스 Edge가 한 장씩 그리고 Krita의 ffmpeg(libopenh264)로 묶는다 — 궁극기 프리비즈(`Tools/Previz/PawnRushUltimates`, JY-skill-lab)와 같은 방식. 브라우저로 볼 때는 정적 서버로 띄우고 `index.html?clip=hand`(스페이스: 멈춤, 화살표: 앞뒤, `&t=5`: 그 순간 정지).

## 파일

- `kit.js` — 기물(눈 · 공 손), 왕관, 손, 하늘, 재질: 궁극기 프리비즈의 `stage.js` · `casts.js`에서 복사(이 브랜치만으로 돌도록).
- `hall.js` — B 대성당(바닥 · 벽 · 창 · 기둥 · 융단 · 큰 계단 · 팀 계단 · 발판 · 왕좌 · 큰 문 · 등불)과 엔드게임 코스 한 토막.
- `hud.js` — 게임 HUD 흉내(위 띠, 차지전 상자, 이름표, 내 기물)와 프리비즈 글(누구의 화면, 설명, ♪, 이름 화면, 끝 카드).
- `stage.js` — 렌더러 · 빛 · 효과 묶음 · 한 프레임. `clips.js` — 다섯 클립의 시간표. `main.js` — `window.PV`(녹화기용)와 재생기.
