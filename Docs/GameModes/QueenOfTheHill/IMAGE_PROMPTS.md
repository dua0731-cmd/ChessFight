# 퀸 오브 더 힐 — 레벨 디자인 예시 이미지 프롬프트 (3차, 6장)

[DESIGN.md §3](DESIGN.md)의 **"왕관 탑"**(물 위의 64 m 탑, 양쪽 출발 다리)을 보여 주는 레벨 디자인 예시다(2026-09-26, R28).

**개정 이력**
| 차수 | 결과 | 사용자 평가 |
|---|---|---|
| 1차 | 섬 위 피라미드 | 폐기. 넓기만 하고, 그리스 여행지 같고 유치하며, 체스와 무관 |
| 2차 | 서재 테이블 체스판 위 등뼈 탑 | 게임 설계 방향은 맞음. 하지만 탑이 작고 고풍스러움 |
| **3차** | 사용자 스케치 배치(가운데 탑, 아래 물, 양쪽 출발 다리) + 사용자 아트 컨셉 | — |

**스타일 기준 = 사용자의 공식 아트 컨셉 이미지 두 장**(기물 6종 라인업, 킹 러시 한 장 요약). 가능하면 이 두 장을 이미지 생성기에 **참고 이미지로 함께 올리고** 프롬프트를 쓴다.

**쓰는 법**
- 모든 프롬프트 끝에 같은 STYLE이 붙어 있다. 가로 16:9를 권한다.
- 1번을 먼저 만들고, 같은 대화에서 "same tower, same art style as the previous image"를 덧붙여 나머지를 만든다.
- 팀 색은 컨셉 이미지대로 **파랑 vs 빨강**으로 썼다. 게임 코드는 아직 주황팀이다(DESIGN §3.8).

---

## 1. 전체 배치 — 물 위의 왕관 탑과 양쪽 출발 다리 (사용자 스케치 구도)

```text
Wide side view of a competitive party-game map: a colossal square castle tower rises straight out of calm blue water in the center, about sixty times taller than the small chess-pawn characters and very wide and massive, tapering slightly toward the top. Its base at the waterline is a band of dark ebony stone; above it the tower is built from ivory and light grey castle stone. The tower is clearly divided into stacked, themed sections: a moat plinth with climbable handholds and hanging ropes, a gatehouse courtyard level, four giant rook-shaped turrets on the corners, an open floor of huge tilting checkerboards, giant knight-head statues jutting out with floating L-shaped platforms, a giant clock face with brass gears, bishop-mitre shaped domes, and on the very top a huge golden queen's crown with a glowing jewel. On the far left, a wooden start pier on stilts above the water holds the red team pawns under red crown banners; on the far right, an identical pier holds the blue team under blue crown banners; each pier has a lowered drawbridge reaching the tower base. Tiny pawns are already climbing on both faces. STYLE: bright stylized 3D party-game render, sunny fantasy chess kingdom, chunky faceted chess-piece characters in ivory white and ebony black with a colored collar ring (blue team or red team), round black ball hands and stubby black feet, faceless or minimal faces like chess figurines, medieval castle stonework in warm ivory and grey, blue and red banners with a gold crown emblem, checkerboard floors, brass gears and iron chains, vivid blue sky with soft clouds, clear blue water, soft global illumination, clean readable level-design composition, no islands, no palm trees, no waterfalls, no text, 16:9
```

## 2. 단면도 — 구간 S1~S8과 팀 반쪽

탑을 반으로 자른 모습이다. 아래 구간은 가운데 코어가 두 팀을 나누고, S4에서 처음 트이고, S7~S8에서 완전히 합쳐진다.

```text
Clean 3D cutaway cross-section of a colossal castle tower standing in water, drawn like a game level-design diagram, showing eight stacked sections from the waterline to the crown. A thick central stone core divides the lower sections into a left half for the red team and a right half for the blue team, each half with its own stairs, climbing walls, hanging ropes and small grassy resting balconies; narrow archways at the far edges connect the halves. The fourth section is a wide open floor of huge tilting checkerboards where both halves meet. Higher sections show knight-head statues with L-shaped jump platforms and a catapult, then the inside of a giant clock with turning brass gears and a rising weight platform, then slippery bishop-mitre domes around a shared balcony, and at the top a golden queen's crown whose points can be climbed to a glowing jewel plate. Tiny red and blue pawns demonstrate each route. Orderly, evenly lit, easy to read. STYLE: bright stylized 3D party-game render, sunny fantasy chess kingdom, chunky faceted chess-piece characters in ivory white and ebony black with a colored collar ring (blue team or red team), round black ball hands and stubby black feet, faceless or minimal faces like chess figurines, medieval castle stonework in warm ivory and grey, blue and red banners with a gold crown emblem, checkerboard floors, brass gears and iron chains, vivid blue sky with soft clouds, clear blue water, soft global illumination, clean readable level-design composition, no islands, no palm trees, no waterfalls, no text, 16:9
```

## 3. 초반 — S1 해자 기단에서 S3 룩 포탑까지

도개교를 건너 기단을 오르고, 성문 안뜰의 회전 원판을 건너, 모서리 룩 포탑을 오르는 구간이다. 팀별 반쪽이라 적은 멀리 보인다.

```text
Three-quarter view of the lower levels of a colossal chess castle tower rising from blue water, seen from the blue team's side. Blue-collared pawns run across a lowered drawbridge from their wooden pier, then climb a dark ebony stone plinth using handholds, a wide ramp, and thick hanging ropes. Above, inside a gatehouse courtyard, a large rotating checkered disc platform carries pawns across a gap while a heavy portcullis rises and falls. At the corner stands a giant ivory rook-shaped turret: pawns climb its outer wall, others take a spiral staircase inside it, and a swaying rope bridge links it to the next turret. A grassy resting spot sits on top of the turret between crenellations with a view over the water. In the distance, beyond the tower's central core, red pawns climb their own half. STYLE: bright stylized 3D party-game render, sunny fantasy chess kingdom, chunky faceted chess-piece characters in ivory white and ebony black with a colored collar ring (blue team or red team), round black ball hands and stubby black feet, faceless or minimal faces like chess figurines, medieval castle stonework in warm ivory and grey, blue and red banners with a gold crown emblem, checkerboard floors, brass gears and iron chains, vivid blue sky with soft clouds, clear blue water, soft global illumination, clean readable level-design composition, no islands, no palm trees, no waterfalls, no text, 16:9
```

## 4. 첫 충돌 — S4 기울어진 체스판

층 전체가 트인 개활지다. 거대한 체스판들이 기울고, 거대한 폰 석상이 정해진 길로 굴러온다. 두 팀이 처음 부딪치고, 가장자리 지붕 회랑은 느리지만 안전하다.

```text
Wide gameplay shot of an open floor halfway up a colossal chess castle tower, high above blue water: several huge black-and-white checkerboard platforms slowly tilt like seesaws, and a giant rolling pawn statue trundles along a track across the floor, knocking characters over. Red-collared and blue-collared pawns clash in the open, grabbing and tackling in floppy ragdoll poses. A red rook character charges in a straight line, bowling blue pawns aside; a red bishop character hovers above tossing small stone shards. Along the edge, a covered stone gallery with arches offers a slower, protected path where a few pawns sneak past. The sea and the two start piers are visible far below. STYLE: bright stylized 3D party-game render, sunny fantasy chess kingdom, chunky faceted chess-piece characters in ivory white and ebony black with a colored collar ring (blue team or red team), round black ball hands and stubby black feet, faceless or minimal faces like chess figurines, medieval castle stonework in warm ivory and grey, blue and red banners with a gold crown emblem, checkerboard floors, brass gears and iron chains, vivid blue sky with soft clouds, clear blue water, soft global illumination, clean readable level-design composition, no islands, no palm trees, no waterfalls, no text, 16:9
```

## 5. 중반 — S5 나이트 도약장과 S6 시계탑 기어

탑 밖으로 튀어나온 말 머리 석상 사이를 L자 발판으로 뛰고, 투석기로 한 번에 날아오르기도 한다. 그 위는 거대한 시계 속이고, 톱니와 오르내리는 추를 타고 오른다.

```text
Dramatic upward view along the outside of a colossal chess castle tower: giant knight-head statues jut out of the walls, connected by floating stone platforms arranged in L-shaped steps like a chess knight's move, with pawns leaping between them. A wooden catapult on a ledge launches a blue pawn high into the air toward the next level, arms flailing. Higher up, a giant clock face is built into the tower with an open side revealing huge turning brass gears that pawns ride upward, a swinging pendulum, and a heavy counterweight platform rising and falling on a chain. A small grassy balcony on top of a knight statue lets a pawn rest and look out over the sea. STYLE: bright stylized 3D party-game render, sunny fantasy chess kingdom, chunky faceted chess-piece characters in ivory white and ebony black with a colored collar ring (blue team or red team), round black ball hands and stubby black feet, faceless or minimal faces like chess figurines, medieval castle stonework in warm ivory and grey, blue and red banners with a gold crown emblem, checkerboard floors, brass gears and iron chains, vivid blue sky with soft clouds, clear blue water, soft global illumination, clean readable level-design composition, no islands, no palm trees, no waterfalls, no text, 16:9
```

## 6. 정상 — S7 비숍 돔과 S8 왕관

두 팀이 완전히 합쳐지는 발코니 위로 비숍 모자 모양 돔들이 있고, 그 위의 거대한 왕관을 오른다. 테두리에서 기물로 승격하고, 가운데 보석 판에서 퀸이 된다.

```text
Low-angle hero shot of the summit of a colossal chess castle tower above the sea: a ring-shaped balcony surrounded by slippery bishop-mitre domes where red and blue pawns wrestle for position. Above it rises a huge golden queen's crown with eight tall points; pawns climb the grooves between the points and swing along iron chains linking them. On the crown's rim a glowing ring transforms a pawn into a knight piece in a burst of light. In the center of the crown, a pawn stands on a glowing jewel plate with a circular capture meter filling around it, becoming the queen, while enemies reach up to drag it down. Far below, the calm blue water and the two start piers with red and blue banners. STYLE: bright stylized 3D party-game render, sunny fantasy chess kingdom, chunky faceted chess-piece characters in ivory white and ebony black with a colored collar ring (blue team or red team), round black ball hands and stubby black feet, faceless or minimal faces like chess figurines, medieval castle stonework in warm ivory and grey, blue and red banners with a gold crown emblem, checkerboard floors, brass gears and iron chains, vivid blue sky with soft clouds, clear blue water, soft global illumination, clean readable level-design composition, no islands, no palm trees, no waterfalls, no text, 16:9
```
