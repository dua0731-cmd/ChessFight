# 퀸 오브 더 힐 — 레벨 장면 이미지 프롬프트 (6차, 6장)

[DESIGN.md §3](DESIGN.md)의 **"하늘 궁전"**을 보여 주는 장면들이다(2026-09-26, R32). 아트는 사용자가 고른 **이미지 A를 베이스로 B·C·D를 살짝** 섞었다([ART_CONCEPTS §5](ART_CONCEPTS.md)).

**이번에 반영한 것**
| 반영 | 내용 |
|---|---|
| 팀·캐릭터 색 | **백팀 = 흰 폰, 흑팀 = 검은 폰**. 색 목깃 없음 |
| 맵 색 | **흑백 없음.** 크림·꿀빛 대리석, 라피스 블루 × 연금색 체크무늬, 금, 옥빛 정원 |
| 출발 갈고리 | 정해진 지점 없이 **폰마다 스스로 던진 갈고리가 부채꼴로 여러 지점에 박힌다.** 모두 다리보다 같거나 높은 곳 |
| 빨리 오르는 시스템 | 탈것(곤돌라, 떠오르는 석판, 궤도 고리, 톱니, 시계추, 움직이는 대계단, 부유섬), **개척자가 여는 길**(종을 치면 빛의 기둥 한 칸이 열림), 체크포인트 |

**쓰는 법**
- 모든 프롬프트에 **같은 세 블록(STRUCTURE, BRIDGES, STYLE)**을 넣었다. 장면만 다르다.
- 1번을 먼저 만든다. 나머지는 같은 대화에서 "same palace, same bridges, same style as the first image"를 덧붙인다. **이미지 A를 참고로 올리면 가장 좋다.**
- 1번은 세로(9:16)도 좋다.
- 폰이 또 한 줄로 서서 한 지점으로 밧줄을 걸면 끝에 "each pawn has its own rope to a different point; there is no single shared anchor"를 한 번 더 붙인다.

**고정 블록**

> STRUCTURE: an immense otherworldly sky palace rising from sheer waterfall cliffs in the middle of the sea, around a hundred and sixty times taller than a pawn and very wide, yet clearly soaring upward. Its body is graceful warm cream and honey-colored marble — arches, colonnades, bridges and grand palace staircases stacked on many levels around a tall glass column of light at its center that holds an elevator. Colossal checkered cubes in lapis blue and pale gold hang from golden chains and float beside it, checkered rings in lapis blue and gold slowly orbit its middle inside a layer of clouds, and golden L-shaped launch pads and golden diagonal rails link the levels. One side of the upper body is a clockwork quarter with giant golden gears, a huge swinging clock pendulum and thin celestial rings. Above it, floating masses of rock carry small green gardens with cypress trees, bound by golden chains. At the very top, a vast ring-shaped gate glows with the starry light of another world, with a giant golden crown floating at its center and a beam of light rising into the sky. Small jade-green garden terraces with little cypress trees on every level serve as resting spots. No black-and-white checker patterns anywhere and no pure white stone.
>
> BRIDGES: two long stone arch bridges reach toward the palace from rocky islets on opposite sides, the white team on the left and the black team on the right; each bridge ends in a wide, crumbling broken edge about twenty meters short of the palace. Spread out along the broken edge, every pawn has thrown its own grappling hook, so many separate ropes fan out from each bridge to many different points on the palace's lower arches, railings and terraces, all at the same height as the bridge or higher, and the pawns are reeled upward along their own ropes from many different directions.
>
> STYLE: stylized 3D party-game screenshot, bright sunny sky with soft white clouds, sparkling turquoise sea, small green rocky islets on the horizon, clean pastel palette, soft shadows, cheerful Fall Guys-like mood; the characters are cute chunky chess pawns — the white team glossy pure white, the black team glossy pure black, no colored collars — with round ball hands, stubby feet and floppy ragdoll poses, tiny compared with the palace; crisp, no text, no signs, no watermark

---

## 1. 전경 — 바다 위의 하늘 궁전

바다 높이에서 올려다본 전체 모습이다. 양쪽 다리에서 갈고리 밧줄이 부채꼴로 뻗는다.

```text
Low-angle wide shot from just above the sea, the whole palace in view from the waterfall cliffs to the crown. STRUCTURE: an immense otherworldly sky palace rising from sheer waterfall cliffs in the middle of the sea, around a hundred and sixty times taller than a pawn and very wide, yet clearly soaring upward. Its body is graceful warm cream and honey-colored marble — arches, colonnades, bridges and grand palace staircases stacked on many levels around a tall glass column of light at its center that holds an elevator. Colossal checkered cubes in lapis blue and pale gold hang from golden chains and float beside it, checkered rings in lapis blue and gold slowly orbit its middle inside a layer of clouds, and golden L-shaped launch pads and golden diagonal rails link the levels. One side of the upper body is a clockwork quarter with giant golden gears, a huge swinging clock pendulum and thin celestial rings. Above it, floating masses of rock carry small green gardens with cypress trees, bound by golden chains. At the very top, a vast ring-shaped gate glows with the starry light of another world, with a giant golden crown floating at its center and a beam of light rising into the sky. Small jade-green garden terraces with little cypress trees on every level serve as resting spots. No black-and-white checker patterns anywhere and no pure white stone. BRIDGES: two long stone arch bridges reach toward the palace from rocky islets on opposite sides, the white team on the left and the black team on the right; each bridge ends in a wide, crumbling broken edge about twenty meters short of the palace. Spread out along the broken edge, every pawn has thrown its own grappling hook, so many separate ropes fan out from each bridge to many different points on the palace's lower arches, railings and terraces, all at the same height as the bridge or higher, and the pawns are reeled upward along their own ropes from many different directions. STYLE: stylized 3D party-game screenshot, bright sunny sky with soft white clouds, sparkling turquoise sea, small green rocky islets on the horizon, clean pastel palette, soft shadows, cheerful Fall Guys-like mood; the characters are cute chunky chess pawns — the white team glossy pure white, the black team glossy pure black, no colored collars — with round ball hands, stubby feet and floppy ragdoll poses, tiny compared with the palace; crisp, no text, no signs, no watermark
```

## 2. 출발 — 부채꼴 갈고리와 앙파상

흰 폰들이 부서진 다리 끝에 퍼져 서서 각자 갈고리를 던진다. 먼저 올라선 검은 폰이 흰 폰 한 명의 갈고리를 떼어 내서, 그 흰 폰이 바다로 떨어진다.

```text
Close three-quarter view at the base of the palace, looking slightly upward. On the left, the wide, crumbling broken edge of a stone arch bridge; white pawns stand spread along it, several of them mid-throw, grappling hooks flying in arcs, and many separate ropes already fan out from different spots on the bridge to different points on the palace's lower cream-marble arches, railings and garden terraces, every point at the bridge's height or higher, with white pawns being reeled upward along their own ropes. On one terrace, a black pawn that arrived earlier pries a white pawn's hook loose with a comic spark; the white pawn falls toward the turquoise sea with a big splash, while the others land and run for a grand staircase. Sheer waterfall cliffs below, the palace continuing far above. STRUCTURE: an immense otherworldly sky palace rising from sheer waterfall cliffs in the middle of the sea, around a hundred and sixty times taller than a pawn, warm cream and honey-colored marble arches, colonnades and grand staircases around a glass column of light, lapis-blue and pale-gold checkered cubes on golden chains, small jade-green garden terraces with cypress trees; no black-and-white checker patterns and no pure white stone. STYLE: stylized 3D party-game screenshot, bright sunny sky with soft white clouds, sparkling turquoise sea, small green rocky islets on the horizon, clean pastel palette, soft shadows, cheerful Fall Guys-like mood; the characters are cute chunky chess pawns — the white team glossy pure white, the black team glossy pure black, no colored collars — with round ball hands, stubby feet and floppy ragdoll poses; 16:9, crisp, no text, no signs, no watermark
```

## 3. 아래층 — 움직이는 대계단, 사슬 곤돌라, 개척의 종

움직이는 궁전 대계단과 금사슬에 매달려 오르내리는 체크 큐브 곤돌라. 구간 꼭대기에 먼저 도착한 흰 폰이 금빛 종을 치자, 한가운데 빛의 기둥의 한 칸이 밝게 열려 뒤따르던 흰 폰들이 뛰어든다.

```text
Mid-shot of the lower levels of the palace above the sea. A grand cream-marble palace staircase slowly moves upward like an escalator, carrying pawns. Beside it, colossal lapis-blue and pale-gold checkered cubes hang from golden chains and rise and fall like gondolas; pawns leap onto a moving cube while another carries a group upward. At the top of this level, a white pawn that arrived first rings a large golden bell, and in response one segment of the tall glass column of light at the palace's center lights up brightly and its doors open; other white pawns further down rush toward it to ride straight up, while a black pawn races to reach it too. A small jade-green garden terrace with cypress trees offers a resting spot. STRUCTURE: an immense otherworldly sky palace, around a hundred and sixty times taller than a pawn, warm cream and honey-colored marble arches, colonnades and grand staircases around a glass column of light; no black-and-white checker patterns and no pure white stone. STYLE: stylized 3D party-game screenshot, bright sunny sky with soft white clouds, sparkling turquoise sea, clean pastel palette, soft shadows, cheerful Fall Guys-like mood; the characters are cute chunky chess pawns — the white team glossy pure white, the black team glossy pure black, no colored collars — with round ball hands, stubby feet and floppy ragdoll poses; 16:9, crisp, no text, no signs, no watermark
```

## 4. 첫 충돌 — 떠오르는 석판과 구름 속 궤도 고리

나선을 그리며 떠오르는 석판과 금빛 L자 도약대를 지나면, 구름 속에서 돌며 오르내리는 체크 고리가 나온다. 흰 폰과 검은 폰이 같은 고리 위에서 처음 부딪히고, 두 쪽을 잇는 도개교가 내려온다.

```text
Wide shot at the middle of the palace inside a soft layer of clouds. Below, cream-marble slabs float upward in a slow spiral carrying pawns, and golden L-shaped launch pads fling pawns two steps up and one step sideways onto the next slab. Above, large checkered rings in lapis blue and pale gold orbit around the palace's glowing central glass column, slowly rising and falling; white pawns and black pawns meet on the same ring for the first time and wrestle and tackle in floppy ragdoll poses, one pawn swinging across a gap on a hanging rope. Between the two sides of the palace, a golden drawbridge is lowering after a pawn pulled a lever, joining the two halves. Glimpses of the turquoise sea far below through the clouds. STRUCTURE: an immense otherworldly sky palace, around a hundred and sixty times taller than a pawn, warm cream and honey-colored marble arches and colonnades around a glass column of light, small jade-green garden terraces with cypress trees; no black-and-white checker patterns and no pure white stone. STYLE: stylized 3D party-game screenshot, bright sunny sky with soft white clouds, sparkling turquoise sea, clean pastel palette, soft shadows, cheerful Fall Guys-like mood; the characters are cute chunky chess pawns — the white team glossy pure white, the black team glossy pure black, no colored collars — with round ball hands, stubby feet and floppy ragdoll poses; 16:9, crisp, no text, no signs, no watermark
```

## 5. 위층 — 시계 태엽 구역과 공중 정원

한쪽 면이 거대한 시계 장치다. 돌아가는 금빛 톱니가 폰들을 위로 실어 나르고, 시계추에 매달려 틈을 건넌다. 그 위에는 사슬로 묶인 부유섬 정원이 천천히 오르내리고, 개척자가 연 빛의 다리가 섬 사이에 나타난다.

```text
Upward view along the upper body of the palace, above the clouds. On one side, a clockwork quarter: giant golden gears turn and carry pawns upward on their teeth, a huge golden clock pendulum swings across a gap with pawns clinging to it, thin celestial rings circle overhead, and a coiled golden spring launches a small lift platform upward. Above, floating masses of rock with little jade-green cypress gardens, bound by golden chains, slowly rise and fall; a bridge made of light is appearing between two floating gardens, opened by a pawn at the top. White and black pawns race and shove across these rides. STRUCTURE: an immense otherworldly sky palace, around a hundred and sixty times taller than a pawn, warm cream and honey-colored marble arches and colonnades around a glass column of light, lapis-blue and pale-gold checkered cubes on golden chains; no black-and-white checker patterns and no pure white stone. STYLE: stylized 3D party-game screenshot, bright sunny sky with soft white clouds, a sea of clouds below, clean pastel palette, soft shadows, cheerful Fall Guys-like mood; the characters are cute chunky chess pawns — the white team glossy pure white, the black team glossy pure black, no colored collars — with round ball hands, stubby feet and floppy ragdoll poses; 16:9, crisp, no text, no signs, no watermark
```

## 6. 정상 — 궁전 대발코니와 하늘의 문

열주가 둘러싼 거대한 원형 발코니에서 두 팀이 맞붙는다. 그 위 하늘의 문으로 나타났다 사라지는 빛의 계단이 이어지고, 문 가장자리에서 기물로 승격하며, 문 한가운데 떠 있는 왕관 아래 보석 판에서 퀸이 된다.

```text
Upward hero shot of the summit of the palace, far above the clouds. A vast circular palace balcony ringed by cream-marble colonnades, where white and black pawns clash and wrestle for position. Above it rises a vast ring-shaped gate glowing with the starry light of another world, with a giant golden crown floating at its center and a beam of light rising into the sky. Glowing light steps that appear and fade, and golden chains, lead up from the balcony to the gate. On the gate's rim, a pawn transforms into a knight chess piece in a burst of light; beneath the crown, a pawn stands on a glowing jewel plate with a circular capture meter filling around it, becoming the queen, while rivals reach up to pull it down. STRUCTURE: an immense otherworldly sky palace, warm cream and honey-colored marble with lapis-blue and pale-gold checker details and gold trims; no black-and-white checker patterns and no pure white stone. STYLE: stylized 3D party-game screenshot, bright sunny sky with soft white clouds, a sea of clouds below, clean pastel palette, soft shadows, cheerful Fall Guys-like mood; the characters are cute chunky chess pawns — the white team glossy pure white, the black team glossy pure black, no colored collars — with round ball hands, stubby feet and floppy ragdoll poses; 16:9, crisp, no text, no signs, no watermark
```
