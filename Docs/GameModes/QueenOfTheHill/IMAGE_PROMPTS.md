# 퀸 오브 더 힐 — 레벨 디자인 예시 이미지 프롬프트 (4차, 6장)

[DESIGN.md §3](DESIGN.md)의 **"왕관 첨탑"**을 보여 주는 레벨 디자인 예시다(2026-09-26, R29).

**개정 이력**
| 차수 | 결과 | 사용자 평가 |
|---|---|---|
| 1차 | 섬 위 피라미드 | 구조는 폐기. **바다·다리·전체 맵 분위기는 좋았음** → 4차 환경에 되살림 |
| 2차 | 서재 테이블 위 가늘고 긴 탑 | **탑이 마음에 듦** → 4차 탑의 기준 |
| 3차 | 40×40 m 사각 성 | 폐기. 뚱뚱한 성, 컨셉 이미지를 억지로 따라 디자인이 바뀜 |
| **4차** | 2차 탑을 크고 길쭉하게 + 신비로운 체스 비주얼, 1차의 바다·다리, 끊긴 다리에서 밧줄로 건넘 | — |

**쓰는 법**
- 탑이 이미지마다 달라지지 않게, 모든 프롬프트에 **같은 탑 설명(TOWER)**과 **같은 스타일(STYLE)**을 넣었다.
- 1번을 먼저 만든다. 나머지는 같은 대화에서 "same tower and same style as the first image"를 덧붙여 만든다.
- 이번에는 **컨셉 이미지를 참고로 올리지 않는 것**을 권한다. 3차처럼 탑이 성으로 바뀔 수 있다. 캐릭터만 참고가 필요하면 기물 라인업 한 장만 올린다.
- 2차 탑 이미지가 남아 있다면, 그것을 참고로 올리고 "make this tower much taller and bigger, keep its design"을 덧붙이는 것이 가장 확실하다.
- 가로 16:9, 팀 색은 파랑 vs 빨강(게임 코드는 아직 주황, DESIGN §3.8).

---

## 1. 전체 배치 — 바다 한가운데 첨탑과 끊긴 두 다리 (사용자 스케치 구도)

```text
Wide side view of a party-game map in the middle of a bright sunny sea. TOWER: in the center stands one colossal, very tall and slender magical spire, about sixty-four times taller than a pawn and four times taller than it is wide — not a castle, not a wide fortress. It is built from giant chess elements stacked irregularly and asymmetrically around one central column: at the waterline a heap of toppled giant chess pieces and tilted chessboards; above it one giant ivory rook section with crenellations and hanging ropes; black-and-white checkered ramps and plank bridges zigzagging up the outside; a ring of floating checkerboard tiles slowly orbiting the column; a giant ebony knight head and a giant ivory knight head jutting out on opposite sides with small floating L-shaped platforms; a brass spiral staircase wrapped around the column with a huge swinging brass clock pendulum; small ledges with green felt floors and tiny roofs for resting; a round balcony near the top; and above the spire a giant golden queen's crown floating in the air with a glowing jewel and a soft beam of light rising into the sky. Mystical and surreal: loose stones, chess squares and small chess pieces float around the upper tower, and the checker seams glow faintly. BRIDGES: two long stone arch bridges reach toward the tower from small rocky islets on opposite sides — red team pawns on the left bridge, blue team pawns on the right — and each bridge ends abruptly in a broken, crumbled edge well short of the tower, leaving an open gap above the water; from the broken ends pawns fire ropes to glowing golden anchor rings at the tower's base and zip across. STYLE: stylized 3D party-game screenshot, bright sunny sky-blue background with soft white clouds, sparkling turquoise sea, small green rocky islets far on the horizon, clean pastel palette, soft shadows, cheerful Fall Guys-like mood, cute chunky chess-piece characters in ivory white and ebony black with a blue or red collar ring, round black ball hands and stubby feet, floppy ragdoll poses, 16:9, crisp, no text, no signs, no watermark
```

## 2. 출발 — 끊긴 다리, 2칸 이동 밧줄, 앙파상

파랑팀 폰들이 끊긴 다리 끝에서 탑 아래 고리로 밧줄을 걸고 건너간다. 착지 턱에서 기다리던 빨강팀 폰이 한 명의 밧줄을 풀어 버리고, 그 폰은 바다로 떨어진다.

```text
Close three-quarter view of the base of the tower from TOWER (a colossal slender magical chess spire rising from the sea, built from stacked giant chess elements around a central column). On the right, the broken, crumbled end of a stone arch bridge full of blue-collared pawns; they fire ropes across a fifteen-meter gap to glowing golden anchor rings on a landing ledge at the tower's base, which sits on a heap of toppled giant chess pieces and tilted chessboards, and zip across, stubby arms clutching the ropes. On the landing ledge a red-collared pawn yanks one rope loose with a comic spark; its blue rider drops toward the turquoise water with a big splash below, while other blue pawns land safely on the heap and start climbing. Playful slapstick, no injury. STYLE: stylized 3D party-game screenshot, bright sunny sky-blue background with soft white clouds, sparkling turquoise sea, small green rocky islets far on the horizon, clean pastel palette, soft shadows, cheerful Fall Guys-like mood, cute chunky chess-piece characters in ivory white and ebony black with a blue or red collar ring, round black ball hands and stubby feet, floppy ragdoll poses, 16:9, crisp, no text, no signs, no watermark
```

## 3. 아래층 — 잡힌 말 더미, 룩 성벽, 체크무늬 비탈 (길 고르기)

더미 꼭대기에 선 폰의 어깨 너머 시점. 룩 성벽의 세 갈래(쉼턱 있는 통벽, 흉벽의 밧줄, 안쪽 나선 계단)와 그 위를 지그재그로 오르는 체크무늬 경사로가 보인다.

```text
Over-the-shoulder view of a blue-collared pawn standing on top of a heap of toppled giant chess pieces at the foot of a colossal slender magical chess spire in the sea. Directly ahead rises a giant ivory rook section wrapped around the central column, offering three routes: a sheer climbable wall with a small resting notch halfway, thick ropes hanging from the crenellations, and an arched doorway to a spiral staircase inside the rook. On top of the rook, a green felt resting ledge between the crenellations where a red-collared pawn waits to shove climbers. Above that, black-and-white checkered ramps and plank bridges zigzag up the outside of the spire, and a giant pawn statue rolls down one ramp on a set path. Floating chess squares drift in the air around the upper tower. STYLE: stylized 3D party-game screenshot, bright sunny sky-blue background with soft white clouds, sparkling turquoise sea, small green rocky islets far on the horizon, clean pastel palette, soft shadows, cheerful Fall Guys-like mood, cute chunky chess-piece characters in ivory white and ebony black with a blue or red collar ring, round black ball hands and stubby feet, floppy ragdoll poses, 16:9, crisp, no text, no signs, no watermark
```

## 4. 첫 충돌 — S4 떠도는 체스판

코어 둘레를 천천히 도는 체스판 조각들 위에서 두 팀이 처음 만난다. 빨강 룩이 판 위를 직선으로 돌진한다.

```text
Mid-height view of a colossal slender magical chess spire high above a turquoise sea: a ring of large floating black-and-white checkerboard tiles slowly orbits around the tower's central column, their seams glowing faintly, like a surreal carousel. Red-collared and blue-collared pawns jump between the moving tiles and clash on the same tile, grabbing and tackling in floppy ragdoll poses. A red rook chess-piece character charges in a straight line across one tile, bowling blue pawns off the edge; one small tile glows green and hovers still for a moment as a resting spot. Below, the checkered ramps and the rook section; far below, the sea and the two broken bridges. Mystical, playful, readable. STYLE: stylized 3D party-game screenshot, bright sunny sky-blue background with soft white clouds, sparkling turquoise sea, small green rocky islets far on the horizon, clean pastel palette, soft shadows, cheerful Fall Guys-like mood, cute chunky chess-piece characters in ivory white and ebony black with a blue or red collar ring, round black ball hands and stubby feet, floppy ragdoll poses, 16:9, crisp, no text, no signs, no watermark
```

## 5. 중상층 — S5 나이트 도약과 S6 시계추 나선

흑·백 말 머리 사이로 L자 발판을 뛰고, 그 위 황동 나선 계단을 거대한 시계추가 쓸고 지나간다. 계단 사이 그네 밧줄이 지름길이다.

```text
Dramatic upward view along a colossal slender magical chess spire above the sea: a giant ebony knight head and a giant ivory knight head jut out from the central column on opposite sides, with small floating stone platforms arranged in L-shaped steps like a chess knight's move; pawns leap between them, one platform gently bobbing in the air. Higher up, a brass spiral staircase winds around the column while a huge brass clock pendulum swings across it, knocking a pawn off in a floppy pose; a rope swing hangs across a gap between two turns of the stairs as a risky shortcut. A small ledge with a green felt floor and a tiny roof lets a pawn rest and look out over the sea. Floating chess squares and small chess pieces drift around. STYLE: stylized 3D party-game screenshot, bright sunny sky-blue background with soft white clouds, sparkling turquoise sea, small green rocky islets far on the horizon, clean pastel palette, soft shadows, cheerful Fall Guys-like mood, cute chunky chess-piece characters in ivory white and ebony black with a blue or red collar ring, round black ball hands and stubby feet, floppy ragdoll poses, 16:9, crisp, no text, no signs, no watermark
```

## 6. 정상 — 합류 발코니와 떠 있는 왕관

두 팀이 둥근 발코니에서 맞붙고, 그 위에 떠 있는 거대한 왕관으로 사슬과 떠 있는 발판을 타고 오른다. 테두리에서 기물로 승격하고, 가운데 보석 판에서 퀸이 된다.

```text
Low-angle hero shot of the top of a colossal slender magical chess spire high above a turquoise sea: a round balcony circles the central column where red and blue pawns wrestle for position. Above the spire a giant golden queen's crown floats in the air, held by nothing, with a soft beam of light rising from its glowing jewel into the sky; iron chains and small floating stepping stones lead up from the balcony to the crown, and pawns climb them. On the crown's rim a glowing ring turns a pawn into a knight piece in a burst of light. In the center of the crown a pawn stands on the glowing jewel plate with a circular capture meter filling around it, becoming the queen, while enemies reach up to pull it down. Far below, the sea and the two broken stone bridges. STYLE: stylized 3D party-game screenshot, bright sunny sky-blue background with soft white clouds, sparkling turquoise sea, small green rocky islets far on the horizon, clean pastel palette, soft shadows, cheerful Fall Guys-like mood, cute chunky chess-piece characters in ivory white and ebony black with a blue or red collar ring, round black ball hands and stubby feet, floppy ragdoll poses, 16:9, crisp, no text, no signs, no watermark
```
