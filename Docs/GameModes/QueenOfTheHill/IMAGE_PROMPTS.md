# 퀸 오브 더 힐 — 레벨 디자인 예시 이미지 프롬프트 (5차, 6장)

[DESIGN.md §3](DESIGN.md)의 **"퀸의 첨탑"**(128 m, 퀸 기물 모양, 행마를 탈것으로 바꾼 구간들, 위로 끌어 올리는 출발 밧줄)을 보여 주는 레벨 디자인 예시다(2026-09-26, R30).

**개정 이력**
| 차수 | 결과 | 사용자 평가 |
|---|---|---|
| 1차 | 섬 위 피라미드 | 구조 폐기. 바다·다리·맵 분위기는 좋음 |
| 2차 | 서재 테이블 위 가늘고 긴 탑 | 체스로 만든 탑 방향은 좋음 |
| 3차 | 뚱뚱한 사각 성 | 폐기 |
| 4차 | 2차 탑을 그대로 키움 | 폐기. 더 우뚝·훨씬 높게, 가로·세로도 더 크게, 2차 복사 말고 오르는 재미를 위해 새로 디자인. 밧줄은 위로 |
| **5차** | 새 디자인 "퀸의 첨탑" | — |

**쓰는 법**
- 모든 프롬프트에 **같은 세 블록(TOWER, BRIDGES, STYLE)**이 들어 있다. 이미지마다 탑이 달라지지 않게 하기 위해서다.
- 1번을 먼저 만든다. 나머지는 같은 대화에서 "same tower, same bridges, same style as the first image"를 덧붙인다.
- 참고 이미지는 올리지 않는 것을 권한다. 예전 탑 모양으로 끌려간다. 캐릭터만 필요하면 기물 라인업 한 장만 올린다.
- **높이가 안 느껴지면** 1번만 세로 비율(9:16 또는 2:3)로 만들어 본다. 가로 16:9에서는 탑이 작아지기 쉽다.
- **밧줄이 또 아래로 그려지면** 끝에 "the ropes must rise upward from the bridges to the tower"를 한 번 더 붙인다.

**TOWER / BRIDGES 블록 (모든 프롬프트에 포함됨)**

> TOWER: one colossal soaring spire in the middle of the sea, over a hundred times taller than a pawn, much wider and more massive than an ordinary tower yet clearly a tall vertical tower about three times taller than its widest point, shaped like a gigantic queen chess piece carved from ivory and ebony — a broad round foot rising from the sea on a sheer dark ebony plinth, stepped round terraces on the foot, a long slender body, a waist split into several separate floating stone rings slowly rotating around the core inside a layer of clouds, a flared collar forming a wide circular balcony near the top, and above the head a giant golden queen's crown floating in the air with a glowing jewel and a beam of light into the sky; its surface is wrapped in playful climbing routes that turn chess moves into rides — a straight vertical rook elevator shaft, a spiral of black-and-white checker tiles, knight-head statues with L-shaped launch pads, diagonal bishop rails, two small rook turrets that swap places, stairs, climbable walls with handholds, hanging ropes and chains, small green-felt resting balconies with tiny roofs and brass moving parts; glowing checker seams and floating chess fragments make it magical and surreal; not a castle, not a fortress, not a stack of houses.
>
> BRIDGES: two long stone arch bridges reach toward the tower from small rocky islets on opposite sides, low above the water, each ending abruptly in a broken edge well short of the tower; ropes rise upward from the broken bridge ends to glowing golden anchor rings on the tower's foot terraces, which are clearly higher than the bridges, and pawns are pulled up along the slanted ropes — red team on the left bridge, blue team on the right.

---

## 1. 전경 — 우뚝 솟은 퀸의 첨탑

바다 높이에서 올려다본 구도. 탑이 구름층을 뚫고, 왕관은 구름 위에 떠 있다. 양쪽 끊긴 다리에서 밧줄이 위로 뻗는다.

```text
Low-angle wide shot from just above the sea, looking up at the whole map. TOWER: one colossal soaring spire in the middle of the sea, over a hundred times taller than a pawn, much wider and more massive than an ordinary tower yet clearly a tall vertical tower about three times taller than its widest point, shaped like a gigantic queen chess piece carved from ivory and ebony — a broad round foot rising from the sea on a sheer dark ebony plinth, stepped round terraces on the foot, a long slender body, a waist split into several separate floating stone rings slowly rotating around the core inside a layer of clouds, a flared collar forming a wide circular balcony near the top, and above the head a giant golden queen's crown floating in the air with a glowing jewel and a beam of light into the sky; its surface is wrapped in playful climbing routes that turn chess moves into rides — a straight vertical rook elevator shaft, a spiral of black-and-white checker tiles, knight-head statues with L-shaped launch pads, diagonal bishop rails, two small rook turrets that swap places, stairs, climbable walls with handholds, hanging ropes and chains, small green-felt resting balconies with tiny roofs and brass moving parts; glowing checker seams and floating chess fragments make it magical and surreal; not a castle, not a fortress, not a stack of houses. BRIDGES: two long stone arch bridges reach toward the tower from small rocky islets on opposite sides, low above the water, each ending abruptly in a broken edge well short of the tower; ropes rise upward from the broken bridge ends to glowing golden anchor rings on the tower's foot terraces, which are clearly higher than the bridges, and pawns are pulled up along the slanted ropes — red team on the left bridge, blue team on the right. The pawns are tiny compared with the tower, emphasizing its enormous height; the crown sits far above the clouds near the top of the frame. STYLE: stylized 3D party-game screenshot, bright sunny sky-blue background with soft white clouds, sparkling turquoise sea, small green rocky islets far on the horizon, clean pastel palette, soft shadows, cheerful Fall Guys-like mood, cute chunky chess-piece characters in ivory white and ebony black with a blue or red collar ring, round black ball hands and stubby feet, floppy ragdoll poses, crisp, no text, no signs, no watermark
```

## 2. 단면도 — 여덟 구간과 탈것

탑을 옆에서 자른 레벨 디자인 도면 느낌이다. 아래에서 위로 발 테라스(룩 승강기·폰 계단), 흑백 교대 나선, 나이트 도약대, 구름 속 도는 고리, 비숍 대각 레일, 캐슬링 탑, 목깃 발코니, 떠 있는 왕관이 보인다.

```text
Clean side elevation cutaway of the tower, drawn like a colorful game level-design diagram, the full height visible from the sea to the crown. TOWER: one colossal soaring spire in the middle of the sea, over a hundred times taller than a pawn, much wider and more massive than an ordinary tower yet clearly a tall vertical tower about three times taller than its widest point, shaped like a gigantic queen chess piece carved from ivory and ebony — a broad round foot rising from the sea on a sheer dark ebony plinth, stepped round terraces on the foot, a long slender body, a waist split into several separate floating stone rings slowly rotating around the core inside a layer of clouds, a flared collar forming a wide circular balcony near the top, and above the head a giant golden queen's crown floating in the air with a glowing jewel and a beam of light into the sky; not a castle, not a fortress. The cutaway shows eight stacked sections from bottom to top, each with its own ride and a small green-felt resting balcony: a rook elevator shaft beside wide pawn stairs on the foot terraces; a spiral of black-and-white checker tiles, some solid and some faded out; knight-head statues with L-shaped launch pads flinging pawns upward; the floating rotating rings in the clouds with a rope swing between them; diagonal bishop rails carrying pawns up at an angle past a rolling pawn statue; two small rook turrets swapping places; the wide collar balcony full of pawns; and glowing light steps and chains leading to the floating crown. A thick central core separates a left half for red pawns and a right half for blue pawns in the lower sections; the halves meet at the rings and fully merge at the collar. BRIDGES: at the bottom left and right, broken stone arch bridges with ropes rising upward to anchor rings on the foot terraces, higher than the bridges. STYLE: stylized 3D party-game screenshot, bright sunny sky-blue background with soft white clouds, sparkling turquoise sea, clean pastel palette, soft shadows, cheerful Fall Guys-like mood, cute chunky chess-piece characters in ivory white and ebony black with a blue or red collar ring, round black ball hands and stubby feet, floppy ragdoll poses, crisp, no text, no signs, no watermark
```

## 3. 출발 — 밧줄로 끌려 올라가기와 앙파상

끊긴 다리 끝에서 파랑팀 폰들이 위쪽 착지 고리로 끌려 올라간다. 착지 턱에서 기다리던 빨강팀 폰이 한 명의 밧줄을 풀어 바다로 떨어뜨린다. 착지 턱 위로는 룩 승강기와 폰 계단이 이어진다.

```text
Three-quarter view at the base of the tower, looking slightly upward. On the right, the broken edge of a stone arch bridge low above the turquoise sea, crowded with blue-collared pawns; ropes rise steeply upward from the bridge edge to glowing golden anchor rings on a landing ledge of the tower's round foot terrace, clearly several meters higher than the bridge, and blue pawns are being pulled up along the slanted ropes, stubby arms clutching them. On the landing ledge, a red-collared pawn yanks one rope loose with a comic spark; its blue rider falls toward the sea with a big splash, while other blue pawns reach the ledge. Above the ledge: a straight vertical rook elevator shaft with a platform rising inside it, and wide pawn stairs climbing the stepped round terraces of the queen-piece-shaped tower (ivory and ebony, glowing checker seams, sheer dark ebony plinth below). The colossal spire continues far upward out of frame. STYLE: stylized 3D party-game screenshot, bright sunny sky-blue background with soft white clouds, sparkling turquoise sea, small green rocky islets far on the horizon, clean pastel palette, soft shadows, cheerful Fall Guys-like mood, cute chunky chess-piece characters in ivory white and ebony black with a blue or red collar ring, round black ball hands and stubby feet, floppy ragdoll poses, 16:9, crisp, no text, no signs, no watermark
```

## 4. 아래층 — 흑백 교대 나선과 나이트 도약대

몸통을 감는 체크무늬 나선에서 흰 칸과 검은 칸이 박자에 맞춰 번갈아 단단해진다. 그 위에는 흑·백 말 머리 석상 옆의 L자 도약대가 폰을 위와 옆으로 튕겨 올린다.

```text
Mid-level view along the slender body of the colossal queen-piece-shaped tower (ivory and ebony, glowing checker seams) high above a turquoise sea. A spiral path of large black-and-white checker tiles wraps around the body: the white tiles are solid while the black tiles are faded and translucent, and pawns time their runs across them; one pawn falls through a faded tile onto a green-felt resting balcony below. Higher up, giant ebony and ivory knight-head statues jut out from the tower with glowing L-shaped launch pads that fling pawns two steps up and one step sideways onto floating stone platforms, pawns flailing mid-air. An inner arched corridor offers a slower safe path. Tiny pawns show the huge scale; the tower continues far above into a layer of clouds. STYLE: stylized 3D party-game screenshot, bright sunny sky-blue background with soft white clouds, sparkling turquoise sea, small green rocky islets far on the horizon, clean pastel palette, soft shadows, cheerful Fall Guys-like mood, cute chunky chess-piece characters in ivory white and ebony black with a blue or red collar ring, round black ball hands and stubby feet, floppy ragdoll poses, 16:9, crisp, no text, no signs, no watermark
```

## 5. 첫 충돌 — 구름 속의 갈라진 허리

퀸의 허리가 여러 개의 떠서 도는 고리로 갈라져 있고, 구름층이 그 사이를 흐른다. 두 팀이 같은 고리에 올라 처음 부딪친다. 위로는 구름을 뚫고 햇빛 속으로 비숍 대각 레일이 이어진다.

```text
Surreal wide shot at the waist of the colossal queen-piece-shaped tower, in the middle of a soft layer of clouds: the tower's body is split into several separate floating stone rings, ivory and ebony with glowing checker seams, slowly rotating around the central core at different speeds, with gaps of open sky between them. Red-collared and blue-collared pawns jump from ring to ring and clash on the same ring for the first time, grabbing and tackling in floppy ragdoll poses; one pawn swings across a gap on a hanging rope. Above, the tower breaks out of the clouds into bright sunlight, where diagonal bishop rails carry pawns upward at an angle and a pawn statue rolls down a rail. Far below through the clouds, glimpses of the turquoise sea. Magical, airy, playful. STYLE: stylized 3D party-game screenshot, bright sunny sky-blue background with soft white clouds, sparkling turquoise sea, clean pastel palette, soft shadows, cheerful Fall Guys-like mood, cute chunky chess-piece characters in ivory white and ebony black with a blue or red collar ring, round black ball hands and stubby feet, floppy ragdoll poses, 16:9, crisp, no text, no signs, no watermark
```

## 6. 정상 — 캐슬링 탑, 목깃 발코니, 떠 있는 왕관

두 개의 작은 룩 탑이 자리를 맞바꾸며 폰들을 실어 나른다. 그 위 퀸의 목깃 발코니에서 두 팀이 맞붙는다. 머리 위에 떠 있는 왕관으로 빛의 계단과 사슬이 이어지고, 왕관 한가운데 보석 판에서 퀸이 된다.

```text
Upward hero shot of the top of the colossal queen-piece-shaped tower, far above the clouds. Two small ivory rook turrets on opposite sides of the tower's body glide past each other and swap places like castling, carrying pawns across a gap. Above them, the queen's flared collar forms a wide circular balcony where red and blue pawns wrestle for position. Above the tower's head, a giant golden queen's crown floats in the air with a beam of light rising from its glowing jewel; glowing light steps that appear and fade, and iron chains, lead up from the balcony to the crown, and pawns climb them. On the crown's rim a glowing ring turns a pawn into a knight piece in a burst of light; in the center, a pawn stands on the jewel plate with a circular capture meter filling around it, becoming the queen, while enemies reach up to pull it down. A sea of clouds below, the turquoise sea glimpsed far beneath. STYLE: stylized 3D party-game screenshot, bright sunny sky-blue background with soft white clouds, sparkling turquoise sea, clean pastel palette, soft shadows, cheerful Fall Guys-like mood, cute chunky chess-piece characters in ivory white and ebony black with a blue or red collar ring, round black ball hands and stubby feet, floppy ragdoll poses, 16:9, crisp, no text, no signs, no watermark
```
