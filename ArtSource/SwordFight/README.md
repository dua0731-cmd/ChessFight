# 소드 파이트 맵 부품 — 치수표 (R191, 2026-10-28 시작)

사용자(10-28): 맵을 교체한다. **판처럼 게임 판정에 쓰는 부품은 Blender에서 직접**, 모양이 복잡한 소품은 **Tripo로 만들고 Blender에서 다듬어** 넣는다.
컨셉은 지금 그대로(공중에 뜬 8×8 판 + 6 m 아래 어두운 서재 책상, [DEMO](../../Docs/GameModes/SwordFight/DEMO.md)).
치수는 지금 코드(`Assets/Scripts/RagdollLab/SwordFightArena.cs`, `SwordFightShapes.Pedestal`, `Core/SwordMatch.cs`의 `SwordBoard`)에서 그대로 옮겼다.

순서: **① 이 치수표 확인** → ② Tripo 프롬프트 → ③ Blender 정리 스크립트(폰 러쉬 `ArtSource/PawnRush/clean_tripo_v1.py`와 같은 방식) → ④ Unity 연결 코드(`SwordFightArena`가 상자 대신 프리팹을 놓음).

## 0. 공통 규칙

| 항목 | 규칙 |
|---|---|
| 단위 | 1 단위 = 1 m, 실제 크기로. Apply Transform |
| 기준점(피벗) | **바닥 가운데**(폰 러쉬 부품과 같음). 예외는 표에 따로 적음 |
| 기준 칸 크기 | **2 m로 만든다.** 한 팀이 4명 이상이면 판이 2.5 m 칸이 되는데, 그때는 코드가 칸 · 테 · 난간을 가로 · 세로로만 ×1.25 늘린다(높이는 그대로). 소품은 크기는 그대로, 위치만 ×1.25 |
| 좌표 | 판 가운데가 원점(0, 0, 0), **판 윗면 = 높이 0**. Unity 축(x 오른쪽, y 위, z 앞 = 검은 팀 쪽). a1 칸 = (−7, −7), h8 칸 = (7, 7) |
| 충돌체 | 판 모델에는 필요 없다(서는 바닥은 코드의 숨긴 플랫폼 `SwordFightEdgeFloor`). **룩 받침만** 충돌용 메시가 따로 필요. 난간 충돌체는 코드가 만든다 |
| 재질 | 이름만 나눠 두고 Unity에서 URP 재질로 바꿔 끼운다. **베이스 컬러에 그림자 · 하이라이트를 굽지 않는다**(URP 조명이 한 번 더 그림자를 넣음) |
| 저장 | `ArtSource/SwordFight/<부품>/<부품>_work.blend` + `<부품>.fbx`(Hall 소품과 같은 방식, `.blend` · `.fbx`는 LFS 자동). Tripo 원본 GLB는 `ArtSource/SwordFight/tripo_raw/`. Unity 쪽은 `Assets/Art/SwordFightMap/`(폰 러쉬 `PawnRushMap`과 같은 자리) |

## 1. 판 부품 — Blender에서 직접

64칸이 **한 칸씩** 깜빡이다 떨어지고(`SwordFightArena.Step`), 룩 E가 가장자리 테를 **칸마다** 숨기고, 난간이 칸과 같이 꺼지므로 판은 칸 단위 부품이어야 한다. 코드가 64번 복사해 놓는다.

| 부품 | 크기(m, 가로 x × 높이 y × 깊이 z) | 개수(판 위) | 기준점 · 놓이는 곳 | 비고 |
|---|---|---|---|---|
| `SF_Square_Light` | **1.96 × 0.6 × 1.96** | 32 | 바닥 가운데 → 코드가 높이 −0.6에 놓음(윗면 = 0). 칸 가운데 x · z = −7, −5, … 7 | 칸 사이 틈 4 cm. 모든 모서리 4.5 cm 둥글게(10-28 다듬기, 틈이 홈처럼 보임). 판자는 칸 하나에 약 3.5줄(텍스처 한 장 = 6 m). a1이 밝은 칸 |
| `SF_Square_Dark` | 1.96 × 0.6 × 1.96 | 32 | 같음 | 밝은 칸과 같은 모양, 재질만 다름 |
| `SF_Frame_2m` (판 둘레 테두리, 10-28에 얇은 놋쇠 띠 `SF_Trim_2m`을 바꿈) | 단면 폭 **0.17**(판 안쪽 2 cm 겹침 + 바깥 15 cm) × 높이 **0.66**(−0.62 ~ 판보다 4 cm 위) × 길이 **2**. 바깥 위 모서리 반지름 6 cm로 둥근 월넛, 옆에 청동 줄(높이 3 cm, 7 mm 튀어나옴, −0.165 ~ −0.135) | 32(한 변 8개, 모서리 칸에는 2개) | **기준점 = 판 가장자리 선 위, 바닥(−0.62). 모델의 +X가 판 바깥쪽** → 코드가 변마다 0 · 90 · 180 · −90° 돌려 놓음 | 룩 E가 칸마다 따로 숨김 → **반드시 칸 한 변 길이로 끊는다**. 충돌체 없음(판보다 4 cm 높은 턱은 그림만) |
| `SF_Frame_Corner` (테두리 모서리) | 테두리 단면을 판 모서리 둘레로 90° 돌린 것, 0.177 × 0.177 × 0.66 | 4 | 기준점 = 판 모서리(±8, ±8), 바닥. 기본은 +X · +Y 쪽(오른쪽 위 모서리), 나머지는 90 · 180 · 270° | 모서리 칸(a1 · h1 · a8 · h8)에 딸림: 그 칸이 떨어지거나 룩 E가 숨길 때 같이 |
| `SF_Rail_1p9` (낮은 난간) | **0.14 × 0.42 × 1.9**, 윗면 완전히 둥글게 + 양 끝 청동 마개(0.16 × 0.03 × 0.44, 10-28) | 8(a · h 파일, 3~6랭크) | 바닥 가운데, 높이 0. 칸 가운데에서 바깥쪽으로 0.9 m(x = ±7.9), z = −3, −1, 1, 3 | 칸이 떨어지면 같이 사라짐. 길이 방향 = z |
| 홈 랭크 띠 | 1.8 × 0.12 | 16 | — | **모델 필요 없음**(코드가 그리는 얇은 띠) |

**판 색 (10-28 사용자 결정):** 처음 제안한 세이지 그린은 "밝은 부분 색이 별로" → 밝은 칸 후보 넷 중 사용자가 **밝은 오크**를 고름. 어두운 칸은 사용자가 보낸 월넛 텍스처.

| 재질 이름 | 지금(게임) | 정한 것 | 비고 |
|---|---|---|---|
| `Square_Light` | 메이플 `#ad9c80` | **밝은 오크 `#b09a7c`**: 텍스처 `textures/T_SF_Oak.png`(보낸 두 번째 월넛 그림의 결을 이 색으로 다시 칠함) | **룩 베이지 이펙트가 이 칸 위에서 약하게 보인다**(v1 이펙트 렌더). 룩 이펙트를 바꿀지는 나중에 |
| `Square_Dark` | 월넛 `#5a3d2a` | **보낸 월넛 텍스처** `textures/T_SF_Walnut.png`(첫 번째 그림 그대로, 평균색 약 `#4e4139`) | 원본 WebP는 `textures/source/` |
| `Trim_Bronze` | 놋쇠 `#c9a04c` | 무광 짙은 청동 `#5a4a3a`(제안 그대로, 아직 의견 없음) | 금색은 킹만 |

경고 깜빡임 색(금빛 `#ffbf45` → 청록 제안)은 코드에서 바꾼다(모델과 상관없음).

## 2. 판 위 장애물

| 부품 | 크기(m) | 개수 | 기준점 · 놓이는 곳 | 만드는 법 |
|---|---|---|---|---|
| `SF_RookPedestal` (룩 모양 돌 받침) | 지름 **아래 1.45 · 허리 1.05 · 위 1.36**, 높이 **0.79**(몸통 0.675 + 위 톱니 6개, 톱니 하나 0.24 × 0.11 × 0.24, 반지름 0.53에 둘러섬) | 4 | 바닥 가운데, 높이 0. b2 (−5, −5), g2 (5, −5), b7 (−5, 5), g7 (5, 5) | Tripo 또는 Blender(돌림판 모양이라 직접도 쉬움) |
| `SF_RookPedestal_Col` (충돌용) | 몸통 바깥 윤곽만, 톱니 없이 | 4 | 같음 | **볼록 메시, 255면 이하**(Unity 볼록 충돌체 한계). 받침 지름이 1.45 m를 넘으면 칸(1.96)을 거의 덮으니 넘기지 않기 |

- 받침 위는 비워 둔다(사람이 올라설 수 있음).

## 3. 판 아래 서재 (책상 높이 −6 m)

| 부품 | 크기(m) | 개수 | 기준점 · 놓이는 곳 | 만드는 법 | 비고 |
|---|---|---|---|---|---|
| `SF_Desk` (책상 윗판) | **80 × 1 × 50** | 1 | 바닥 가운데 → 윗면이 −6이 되게(−7에 놓음) | Blender 직접(판 + 반복 나무 텍스처) | 지금은 1.6 m마다 나뭇결 줄. 가장자리는 안개(34~85 m) 속이라 안 보임 |
| `SF_Tray` (떨어진 말 쟁반) | **2.2 × 0.15 × 10** | 2 | 바닥 가운데, (−12, −6, 0.5) 흰 팀 · (12, −6, 0.5) 검은 팀 | Blender 직접 | 말이 z = −3 ~ 5에 1.6 m 간격 6자리로 누움 |
| `SF_BookStack` (책 더미 기둥) | 전체 높이 **5.35 이하**(−6 ~ −0.65, 판 바닥보다 5 cm 아래), 책 한 권 약 3.2 × 0.55~0.91 × 2.4~2.8, 7~8권, 권마다 살짝 돌아감 | 4 | 바닥 가운데. (±6.2, −6, ±6.2) — 판 네 귀퉁이 아래 | **Tripo**(높이가 다른 2~4가지 변형이면 좋음) | 책 색 6가지(검붉음 · 짙은 초록 · 남색 · 황토 · 자주 · 짙은 회색). **높이를 넘기면 판을 뚫음** |
| `SF_ChessClock` (체스 시계) | 몸통 **4.2 × 2.2 × 1.6**, 위 단추까지 2.45 | 1 | 바닥 가운데. (11, −6, −11), 세로축으로 −28.6° 돌림 | **Tripo** → Blender에서 부품 나누기 | 아래 "움직이는 부품" |
| `SF_Lamp` (스탠드 램프) | 기둥 지름 0.24 · 길이 **21**(−6 ~ 15), 팔 지름 0.2 · 길이 7.2, 갓 아래 지름 **3.2** · 높이 1.4(초록 `#1f4a35`), 전구 지름 0.7 | 1 | **기둥 바닥** = (−12.5, −6, 10). 갓 가운데 = (−7, 14.5 ~ 15.9, 8), 즉 기둥에서 x +5.5 · z −2 | **Tripo** → Blender에서 크기 맞춤 | 빛은 Unity Light(코드), 모델에는 빈 물체 `Light_Anchor`를 전구 자리 (−7, 15, 8)에 |

### 움직이는 부품 (따로 떼고 이름 맞추기)

| 부품 | 물체 이름 | 기준점 | 코드가 하는 일 |
|---|---|---|---|
| 체스 시계 | `Body`, `Face_L`, `Face_R`, `Button_L`, `Button_R` | — | 그대로 |
| 체스 시계 바늘 | `Hand_L`, `Hand_R` (길이 0.6, 굵기 0.05 × 0.02) | **회전축**: 시계판 가운데 = 시계 기준점에서 (±1, 1.15, 앞면 0.83) | 경기 시계로 앞면 방향 축을 따라 돌림(1분에 한 바퀴) |
| 시계판 | 지름 1.5, 앞면에 두 개(x = ±1, 높이 1.15) | — | — |
| 램프 | `Light_Anchor` (빈 물체) | 전구 자리 | 스포트라이트를 여기에 붙이고 판 가운데를 보게 함 |

## 4. 비워 둘 공간

| 공간 | 이유 |
|---|---|
| 판 위 높이 0 ~ 10 m, 판 가장자리에서 바깥 4 m까지 | 유령 기물(약 4~5 m, 높이 3.4 m에 뜸)이 지나가고, 플레이어 카메라가 판 둘레를 돈다 |
| 판 아래 −0.6 ~ −6 m, 판 안쪽 | 떨어지는 칸 · 말이 지나간다(책 더미 기둥 4개만 예외) |
| 책상 위 쟁반 둘레 | 떨어진 말이 미끄러져 쟁반으로 간다 |

## 5. 예산 (제안, 처음 넣어 보고 조정)

| 부품 | 삼각형 | 텍스처 | 이유 |
|---|---|---|---|
| 칸(밝은 · 어두운) | ≤ 200 (지금 188) | 반복 텍스처 1024, 두 칸이 같은 텍스처 묶음 | 64개가 늘 화면에 있음 |
| 테두리 · 난간 | 테두리 ≤ 100, 모서리 ≤ 250, 난간 ≤ 300 | 칸과 같은 묶음 | 40개 + 모서리 4 |
| 룩 받침 | ≤ 3,000 | 1024 | 4개, 가까이 보임 |
| 책 더미 | ≤ 3,000 / 개 | 1024 | 멀고 어두움 |
| 체스 시계 · 램프 | ≤ 5,000 / 개 | 1024~2048 | 하나씩, 화면 가장자리 |
| 책상 · 쟁반 | ≤ 500 | 반복 1024 | — |

## 6. 2.5 m 칸 판(한 팀 4명 이상)에서

| 무엇 | 2 m 칸 | 2.5 m 칸 |
|---|---|---|
| 판 크기 | 16 × 16 | 20 × 20 |
| 칸 · 테 · 난간 | 모델 그대로 | 코드가 가로 · 세로 ×1.25(나뭇결도 25% 늘어남 — 거슬리면 월드 좌표 텍스처로) |
| 룩 받침 위치 | ±5 | ±6.25(크기 그대로) |
| 책 더미 · 시계 · 램프 · 쟁반 위치 | 위 표 | ×1.25(크기 그대로). 책 더미는 여전히 판 아래 |

## 7. 레퍼런스 그림 프롬프트 (Blender 부품, 10-28)

사용자(10-28): "Blender로 만들기 전에 블렌더로 만드는 것들 ai 사진을 레퍼런스로 참고 하라고 프롬포트 줘".
그림 AI에 넣는 영어 프롬프트다. **그림은 모양 · 재질 느낌만 참고**하고, 크기 · 개수 · 위치는 위 치수표를 따른다(AI는 8×8 칸 수 · 비율을 자주 틀림).
판 색은 §1 제안(세이지 그린 · 회색 월넛 · 무광 청동)으로 적었다. 지금 색(메이플 · 월넛 · 놋쇠)으로 보고 싶으면 `sage green` → `light maple`, `greyish walnut` → `dark walnut`, `matte dark bronze` → `polished brass`로 바꾼다.

**모든 프롬프트 끝에 붙이는 공통 부분:**
```
stylized realistic game environment art, clean readable shapes, matte materials, soft warm key light, no glowing effects, no magic particles, no characters, no text, no watermark
```
**제외(negative prompt를 받는 AI만):** `glow, neon, sparkles, lens flare, characters, chess pieces on the board, text, logo, blurry, fisheye, extra squares, 10x10 board`

| # | 무엇 | 비율 | 프롬프트 |
|---|---|---|---|
| R1 | 경기장 전체 (분위기 기준 그림) | 16:9 | `A floating 8x8 chessboard arena hovering high above a giant antique study desk in a dark room. The board is made of 64 separate thick square wooden blocks with thin gaps between them, alternating sage green lacquered wood and greyish walnut, framed by a thin matte dark bronze edge. Four short turned-stone pedestals shaped like chess rooks stand near the corners of the board. Low wooden rails run along the middle of the left and right edges. Far below, the dark wooden desk top with two shallow bronze trays. Very dark brown background fading into fog, one warm lamp light from the upper left. High three-quarter view.` |
| R2 | 판 가까이 (칸 · 틈 · 테 · 난간) | 16:9 | `Close-up of the corner of a chessboard made of separate thick square blocks, each block about knee-height thick with slightly rounded top edges and a narrow dark gap between blocks. Alternating sage green lacquered wood and greyish walnut squares with subtle wood grain. A thin matte dark bronze strip runs along the outer edge of the board. A low rounded wooden rail sits on the edge squares. The board floats in darkness. Low camera angle, shallow depth of field.` |
| R3 | 밝은 칸 재질 (텍스처용) | 1:1 | `Seamless tileable texture, top-down flat view of sage green lacquered hardwood, fine straight grain, satin finish, even lighting, no shadows, no highlights, PBR albedo reference` |
| R4 | 어두운 칸 재질 (텍스처용) | 1:1 | `Seamless tileable texture, top-down flat view of greyish walnut hardwood, medium-dark brown with a slight grey tone, fine grain, satin finish, even lighting, no shadows, no highlights, PBR albedo reference` |
| R5 | 룩 받침 (모델링용) | 1:1 | `Game prop concept sheet of a short stone pedestal shaped like a chess rook: wide round base, a narrow waist, a flared round top with six square battlements around the rim. Weathered warm grey stone, simple turned shape, chunky proportions, about as wide as it is tall. Front orthographic view and three-quarter view side by side, neutral grey background, even studio light.` |
| R6 | 판 둘레 테 · 모서리 | 1:1 | `Detail of the edge of a thick wooden board game: a thin matte dark bronze metal strip covering the side of the blocks, slightly taller than the board top, clean corner joint, small screws, subtle wear. Neutral dark background, studio light, close-up product shot.` |
| R7 | 낮은 난간 | 16:9 | `Game prop concept of a low wooden rail, a single rounded beam about knee height, dark varnished wood with a few small bronze end caps, simple and chunky, side view and three-quarter view, neutral grey background, studio light.` |
| R8 | 책상 윗판 · 쟁반 | 16:9 | `Top of a huge antique dark wooden study desk with long planks and deep brown varnish, two long shallow matte dark bronze trays lying on it, a few toppled wooden chess pieces resting in one tray. Dim warm light, dark surroundings, three-quarter view from above.` |

**쓰는 법:** R1 · R2로 분위기와 칸 모양을 정하고 → R3 · R4는 텍스처 참고(그대로 쓸 거면 그림자 없는 것으로 다시 뽑기) → R5~R8은 모델링할 때 옆에 띄워 두고 모양을 본다. 마음에 드는 그림은 `Assets/VFX/Reference`처럼 git에 안 올리는 폴더에 둔다(남의 그림 · AI 그림 라이선스 확인 전).

## 8. Blender 1차 — 판 부품 (v1, 10-28)

사용자(10-28): "tripo로 만들 소모품들은 나중에 넣고 지금은 메인이 되는 경기장 블렌더에서 제작 후 사진으로 보여줘" → "밝은 칸은 밝은 오크(#b09a7c)로 하고 어두운 칸은 보낸 월넛 텍스처로 해서 Blender 렌더해줘".

다시 만들기(Blender 5.2.3, 이 PC에는 10-28 winget으로 설치):
```
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup --python build_arena_v1.py -- <이 폴더>/v1
```

| 결과 | 내용 |
|---|---|
| `v1/SF_*.fbx` 9개 | §1 · §2 · §3의 Blender 직접 부품(10-28 다듬은 뒤): `SF_Square_Light` · `SF_Square_Dark`(각 188 삼각형) · `SF_Frame_2m`(48) · `SF_Frame_Corner`(220) · `SF_Rail_1p9`(276) · `SF_RookPedestal`(744) · `SF_RookPedestal_Col`(92, 볼록) · `SF_Desk`(28) · `SF_Tray`(188). 크기는 Blender가 잰 값으로 치수표와 같음 |
| `textures/T_SF_Walnut.png` · `T_SF_Oak.png` | 1024². UV는 면마다 축 투영, 텍스처 한 장 = 4 m(칸 하나에 판자 약 5줄) |
| `v1/SF_Arena_v1.blend` | 부품(원점, 숨김) + 게임처럼 놓은 판 + 조명 · 카메라. 텍스처는 상대 경로 |
| `v1/SF_Arena_v1_overview.png` | 판 전체, 어두운 서재 조명(해 + 램프 스포트라이트 + 어두운 배경) |
| `v1/SF_Arena_v1_closeup.png` | 가장자리 · 테 · 난간 · 룩 받침 가까이 |
| `v1/SF_Arena_v1_vfx.png` | 이펙트 색 확인: 킹 금 고리 · 룩 베이지 고리 · 비숍 은 X · 퀸 탄 자국 · 경고 청록을 밝은 칸 · 어두운 칸 위에. 발광 세기는 게임과 비슷하게(고리 ×1~1.6) |

- 같은 색 칸도 하나 걸러 90° 돌려 놓아(미리보기) 판자 방향이 다 같지 않다. 게임에서 이렇게 하려면 `SwordFightArena`가 칸을 놓을 때 같은 규칙으로 돌린다(Unity 연결 때).
- 렌더는 Blender 조명이라 Unity URP 화면과 밝기 · 색이 똑같지는 않다. 판단은 Unity에 넣고 다시 본다.
- 이펙트 렌더에서 본 것: 킹 금 · 비숍 은 · 퀸 탄 자국(빨간 테두리 덕분에 월넛 위도 보임) · 경고 청록은 두 칸 모두에서 구분된다. **룩 베이지는 오크 칸 위에서 약하다.**
- 아직 없음: 책 더미 · 체스 시계 · 램프 모델(Tripo, 나중), Unity 프리팹 · 코드 연결.

**다듬기 (10-28, 사용자가 고른 셋 — "판 둘레 테두리, 난간, 칸 모서리·판자"):** 판 둘레를 레퍼런스 5 · 6번처럼 둥근 월넛 테두리 + 청동 줄로(모서리는 둥글게 도는 부품 따로), 난간 윗면을 완전히 둥글게 + 양 끝 청동 마개, 칸 모서리 2 → 4.5 cm, 판자 칸당 약 5 → 3.5줄. 룩 받침은 그대로(높이를 바꾸면 올라서는 판정도 바뀜).

## 9. Unity에 넣음 (10-28, 사용자 "Unity에 넣기")

| 무엇 | 어디 |
|---|---|
| 모델 · 텍스처 | `Assets/Resources/SwordFightMap/`: `SF_Square_Light` · `SF_Square_Dark` · `SF_Frame_2m` · `SF_Frame_Corner` · `SF_Rail_1p9` · `SF_RookPedestal` · `SF_Desk` · `SF_Tray`(FBX) + `T_SF_Oak` · `T_SF_Walnut`(PNG). `v1/`에서 복사(다시 만들면 다시 복사). 충돌용 `SF_RookPedestal_Col`은 안 넣음(충돌은 예전 코드가 만듦) |
| 코드 | `SwordFightArena.Build`가 Play 때 모델을 `Resources.Load`로 놓음(`Model`). 모델이 없거나 `SwordFightArena.UseModels = false`면 예전 상자. 재질은 코드가 만듦(URP Lit + 텍스처, FBX에 딸린 재질은 안 씀) |
| 방향 | FBX를 어떤 축으로 가져오든, 모델마다 한 번 메시 크기를 재서 긴 쪽 = z, 테두리 바깥 · 모서리 = +x · +z가 되게 돌림(`Settle`). 책상만 그 뒤 90° 더 돌려 80 m를 x로 |
| 칸 | 64칸 각각 자기 재질(경고 금빛 깜빡임 그대로). 같은 색 칸 하나 걸러 90° 돌림(판자 방향). 2.5 m 칸이면 가로 · 세로 늘림 |
| 테두리 | 칸 한 변마다 `SF_Frame_2m`, 모서리 칸에 `SF_Frame_Corner`. 룩 E가 숨기는 목록(`BrassIn`)에 들어감. 금속 `Brass`가 청동 `#5a4a3a`로 바뀜(시계 단추 · 램프는 예전 놋쇠) |
| 그대로 | 서는 바닥(숨긴 플랫폼), 난간 · 룩 받침 충돌체, 홈 랭크 띠, 책 더미 · 체스 시계 · 램프(Tripo로 바꿀 때까지), 조명 · 안개 |
| 스킬 이펙트 | 킹 E 박힘 자국이 테두리 바깥 면에(`EdgeOut` 15 cm · `EdgeLip` 4 cm, `SwordFightSkillFx.Breach.cs`). 파편 색 = 청동 + 그 칸 나무 색(오크 `#b09a7c` / 월넛 `#4e4139`). 룩 E가 무너뜨리면 테두리 조각이 **통째로** 떨어짐(나무라 휘지 않고 바깥으로 기울며 돌다가 떨어짐, 나무 조각 3개, `SwordFightArena.LooseFrame` → `SwordFightSkillFx.FallingFrame`). 키트가 없으면 예전 놋쇠 막대 |

확인 목록: [VALIDATION](../../Docs/Network/VALIDATION.md) 맨 위 "R191".

## 10. Tripo 프롬프트 — 서재 소품 (10-28, 사용자 "Tripo 프롬프트")

대상: §3의 책 더미 · 체스 시계 · 램프(지금은 게임 코드가 상자 · 원기둥으로 그림). 룩 받침은 Blender 것으로 정해져 빼었다.
Tripo는 크기를 모르고 만들므로 **크기는 Blender 정리 스크립트가 §3 치수로 맞춘다.** 프롬프트에는 비율만 적었다.

**나눠 만드는 것**(Tripo가 한 덩어리로 만들면 게임이 못 움직이거나 높이를 못 맞추는 것):

| 소품 | Tripo로 | Blender에서 | 이유 |
|---|---|---|---|
| 책 더미 | **책 한 권** 3가지(T1 · T2 · T3) | 스크립트가 7~8권을 쌓아 기둥 4개(높이 5.35 m 이하, 권마다 색 · 크기 · 각도 다르게) | 한 덩어리 더미는 높이를 맞추면 비율이 깨지고, 판을 뚫으면 안 됨 |
| 체스 시계 | **바늘 없는 몸통**(T4) | 바늘 2개(`Hand_L` · `Hand_R`, 축에 기준점)를 직접 | 게임이 바늘을 경기 시계로 돌림 |
| 램프 | **램프 전체**(T5) | 빈 물체 `Light_Anchor`를 전구 자리에 | 빛은 Unity Light(코드) |

**공통으로 붙이는 부분:**
```
stylized realistic game prop, clean shapes, PBR materials, matte finish, neutral even lighting, no baked shadows, no text, no logo, single object, centered
```
**제외(칸이 있으면):** `glow, emissive, gold, shiny chrome, text, letters, logo, multiple objects, base platform, ground plane`

| # | 무엇 | 프롬프트 | 색 · 주의 |
|---|---|---|---|
| T1 | 책 한 권 — 두꺼운 고서 | `a single closed antique leather-bound book lying flat, thick, about one third as tall as it is wide, rounded spine with raised bands, worn deep red leather cover, cream page edges, simple embossed border on the cover` | 게임에서 색을 바꿔 쓰므로 표지는 한 가지 색이면 됨(재질 색으로 6색: 검붉음 · 짙은 초록 · 남색 · 황토 · 자주 · 짙은 회색) |
| T2 | 책 한 권 — 얇은 책 | `a single closed old cloth-bound book lying flat, slimmer, about one sixth as tall as it is wide, flat spine, faded dark green cloth cover, slightly frayed corners, cream page edges` | 쌓을 때 두꺼운 책 사이에 섞음 |
| T3 | 책 한 권 — 큰 장부 | `a single closed large ledger book lying flat, wide and heavy, about one quarter as tall as it is wide, leather corners and spine over a dark navy cloth cover, cream page edges` | 맨 아래 받침으로 |
| T4 | 체스 시계 몸통 | `an antique wooden analog chess clock, a wide box about two and a half times as wide as it is deep, slanted front face with two round white clock dials side by side, no clock hands, two small round push buttons on top, dark walnut wood with small dark bronze fittings` | **바늘 없이.** 시계판은 크림색 원 둘(지름이 몸통 높이의 2/3쯤). 놋쇠 대신 짙은 청동(금색은 킹만) |
| T5 | 스탠드 램프 | `a tall antique floor lamp: a thin straight dark bronze pole, a long angled arm reaching out sideways from the top, a wide green glass cone shade hanging from the end of the arm, a visible bulb inside the shade` | 게임에서는 기둥 21 m · 갓 지름 3.2 m로 늘림 → **길고 가는** 비율이 맞음. 갓은 초록 `#1f4a35` |

**만드는 순서:**
1. 가능하면 그림 AI로 같은 문장의 그림을 먼저 뽑아(흰 배경 · 한 물체) **Tripo 이미지→3D**로 넣는다(글→3D보다 모양이 덜 어긋남). 글→3D만 써도 된다.
2. Tripo 설정: 텍스처 켬(PBR), 가능하면 낮은 폴리곤 / 쿼드 출력. 내려받기는 **GLB**.
3. 받은 파일은 `ArtSource/SwordFight/tripo_raw/`에 이름 그대로: `book_thick.glb` · `book_thin.glb` · `book_ledger.glb` · `chess_clock.glb` · `lamp.glb`.
4. 그다음 AI가 Blender 정리 스크립트로: 크기를 §3에 맞춤 · 기준점 · 삼각형 줄이기(§5 예산) · 그림자 구운 텍스처 확인 · 책 더미 쌓기 · 시계 바늘 · `Light_Anchor` · FBX → Unity 연결.

### Tripo 결과 → 소품 (10-28, 사용자가 GLB 5개를 줌)

받은 모델은 **텍스처 없는 회색**, 삼각형 5만~15만 개(폰 러쉬 때와 같음). 그래서 `clean_tripo_v1.py`가 줄이고 · 크기를 맞추고 · **부위별로 색 칸을 나눈다**. 칸의 재질 이름이 색이다(`SF_rrggbb`, 금속은 `SF_rrggbb_m`) → 게임이 이름을 읽어 색을 만든다(`SwordFightArena.Named`).

```
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --factory-startup --python clean_tripo_v1.py -- <이 폴더>/tripo_raw <이 폴더>/v1
```
(`build_arena_v1.py`를 먼저 돌려 `v1/SF_Arena_v1.blend`가 있어야 미리보기를 그린다. 원본 확인은 `inspect_tripo.py`.)

| 결과 | 크기 · 삼각형 | 나눈 색 |
|---|---|---|
| `SF_Prop_BookStack_A`~`D` | 높이 정확히 **5.35 m**, 바닥 약 3.3~3.7 × 2.8~3.1 m, 4,900 | 책 3종(장부 · 두꺼운 책 · 얇은 책)을 7~8권 쌓음. 권마다 표지 6색 중 하나 + **종이 크림 `#e3d6b8`**(가운데 높이의 등 아닌 세 옆면), 크기 · 각도 · 등 방향 다르게, 높이를 모두 늘려 맨 위가 판 바로 아래 |
| `SF_Prop_ChessClock` | 4.2 × 2.05 × 2.49 m, 6,000 | 나무 `#3a2618`, 문자판 크림 `#efe6d2`, 테 · 위 단추 청동. 빈 물체 `Front`(앞), `Dial_L` · `Dial_R`(문자판 가운데, 반지름 약 0.48 m) |
| `SF_Prop_Lamp` | 높이 27.6 m(갓 아래가 책상 위 21.1 m = 게임 높이 약 15 m), 8,000 | 청동 기둥 · 팔, **초록 갓 `#1f4a35`**. 빈 물체 `Light_Anchor`(갓 아래 가운데). 크기는 기둥에서 갓까지 5.85 m(게임의 램프 자리)로 맞춤 |
| `v1/SF_Arena_v1_study*.png` · `SF_Arena_v1_study.blend` | — | 경기장에 소품을 놓은 렌더 3장(위에서 · 낮게 · 램프 쪽) |

- Tripo 시계에는 바늘이 조각되어 있다(프롬프트는 "바늘 없이"). 문자판을 크림으로 칠해 묻히고, 게임의 움직이는 바늘이 그 위에 그려진다.

**Unity:** `Resources/SwordFightMap`에 6개 더 넣음. `SwordFightArena`가 책 더미 · 시계 · 램프도 모델로(없으면 예전 상자):
- 방향은 모델의 빈 물체로: `Front` / `Light_Anchor` 쪽이 +z가 되게 돌린 뒤, 시계는 예전처럼 −28.6°, 램프는 팔이 판 쪽((5.5, −2))을 보게 110°.
- 시계 바늘은 `Dial_L` · `Dial_R` 위치에(길이 0.4 m), 램프 스포트라이트와 전구 빛 구슬은 `Light_Anchor` 위치에서 판 가운데를 봄.

## 11. 분위기: 램프 빛줄기 · 먼지, 서재 배경 · 달빛, 램프 흔들림 (10-28, 사용자 "추천 1~3 진행")

원칙: **판 위는 깨끗하게**(스킬 이펙트가 보여야 함). 볼거리는 빛 · 배경 · 판 바깥에.
시안: `concept_atmosphere_v1.py`(Blender, `v1/SF_Arena_v1_study.blend`를 열어 더함) → `v1/SF_Arena_v1_atmosphere.png` · `_low.png`.

| # | 무엇 | 게임(`SwordFightArena`) | 고치는 값 |
|---|---|---|---|
| 1 | 램프 빛줄기 | 갓 입구에서 아래로(판 가운데 쪽 12°) 투명한 원뿔 3겹(바깥일수록 옅게 → 가장자리가 부드러움), 따뜻한 `#ffcf8a`가 갓에서 끝으로 사라짐. **판 3 m 위에서 끝남** | `BeamLength` 15 · `BeamAngle` 24° · `BeamAlpha` 0.12 |
| 1 | 먼지 | 빛줄기 안 파티클(Shuriken) 약 140개, 6~10초 동안 천천히 떠다니며 나타났다 사라짐, 크기 3~7 cm | `BeamDust` 140 |
| 2 | 서재 배경 | 판 뒤(z 48) · 양옆(x ±55)에 높이 40 m 책장 벽(책 9줄), 판 뒤 가운데 큰 창문(틀 + 유리 12칸, 안개 받는 Unlit 푸른 회색). 안개(34~85 m) 속이라 실루엣만. 판 크기와 상관없이 고정 | `Room()` |
| 2 | 달빛 | 창문 쪽에서 오는 차가운 방향광 `#8fa8d8`, 세기 0.3, **그림자 없음**(그림자는 램프만). 따뜻한 키 라이트가 계속 주광(`RenderSettings.sun`) | `Room()`의 `moon` |
| 3 | 램프 흔들림 | 램프 · 빛 · 전구 · 빛줄기 · 먼지가 기둥 발에 매달려 함께 흔들림(최대 0.6°, 1.7초 주기, 1.3초에 잦아듦), 빛이 흔들릴 때 최대 12% 어두워짐. 칸이 책상에 떨어질 때 0.35, 룩 E 붕괴 1, 킹 E 착지 0.8. 더 큰 충격이 오면 새로 흔들림. 화면마다 따로(그림만) | `SwayDegrees` · `SwayPeriod` · `SwayDecay`, `Jolt(세기)` |

- **빛이 실제 램프에서 나오게(10-28, 사용자 "실제 랩프에서 조명 효과가 나타나게 해"):** 전구 자리를 모델의 표시점(`Light_Anchor`)이 아니라 **갓 자체(초록 `SF_1f4a35` 칸)의 크기에서 잼**(`SwordFightArena.Shade`: 갓 가운데, 아래에서 35% 높이 = 갓 안). 스포트라이트 · 빛줄기 · 먼지 · 전구가 **갓 입구에서 아래로**(판 가운데 쪽으로 12° 기울임, `LampLean`) 나오고, 퍼짐은 갓 입구만큼 80°(`LampSpread`), 빛줄기는 판 위 3 m까지(약 13.8 m). 램프 방향도 갓 위치로 정함. 갓을 못 찾으면 `Light_Anchor`, 그것도 없으면 예전 자리
- 성능: 새 빛은 달빛 하나(그림자 없음), 배경은 상자 약 140개 · 원뿔 3개 · 파티클 1개. 조명은 굽지 않고 실시간(경기장은 Play 때 지어짐).
- 블렌더 시안의 창문은 게임보다 밝다(Blender에는 안개가 없음). 게임 창문은 안개로 더 어둡다.

## 12. 칸이 책상에 떨어질 때 (10-28, 사용자 "확인 다음 작업" → 아이디어 4)

판 축소로 떨어진 칸이 6 m 아래 책상에 닿는 순간(`SwordFightArena.Step`, 늦게 들어온 사람의 판은 이미 누워 있어 안 함):

| 무엇 | 내용 | 고치는 값 |
|---|---|---|
| 먼지 | 칸 테두리 밑에서 먼지 구름 26개가 책상을 따라 바깥으로 굴러 퍼지고, 조금 떠오르며 커지고 1.4~2.4초에 사라짐(먼지색 `#6e5e4e`, 반투명). 파티클 시스템 하나를 같이 써서 한 줄이 한꺼번에 떨어져도(퀸 28칸) 부담 적음 | `LandDust` 26 |
| 튕김 | 칸이 두 번 작게 튀고 멈춤(처음 25 cm, 0.6초) | `LandBounce` 0.25 · `LandBounceTime` 0.6 |
| 램프 | 전처럼 살짝 흔들림(0.35) | §11 |
| **흔들릴 때 먼지**(10-28, 사용자 "칸이 흔들릴 때도 먼지가 생기게 해줘") | 칸이 떨리는 동안(판 축소 경고의 끝 40%, 룩 E 금 가는 동안): 칸 이음새(윗면 둘레)에서 작은 먼지가 피어오르고(초당 약 7개), 칸 밑면 가장자리에서 고운 가루가 흘러 책상 쪽으로 떨어짐(초당 18개, 중력). 같은 먼지 파티클 시스템 + 가루 시스템 하나 | `TrembleDustRate` 18 |

- 소리는 아직 없음(나중에 "쿵" 효과음을 넣을 자리는 같은 곳).
- **시험하는 법(10-28, 사용자 "내가 칸이 떨어지게 하는 시스템이 없어"):** 판 축소는 경기 씬(`SwordFight.unity`: Play → 기물 고르기 → F9 → 5초 뒤 2:00)에만 있다. 스킬 시험 씬 `SwordFight_ArenaSkillTest`에는 없어서 **K = 다음 칸 묶음 떨어뜨리기**(경기와 같은 계획: 룩 줄 → 비숍 대각선 → 나이트 L → 퀸 가장자리, 1.5초 깜빡인 뒤 떨어짐, 그 칸 바닥도 사라져 서 있던 사람이 떨어짐), **L = 판 되돌리기**를 넣음(`SwordFightArenaStage`, 오프라인일 때만).

## 13. Tripo 2차: 룩 석상 · 아치 창 (10-28)

사용자: "촛불 코드랑 모델 지워주고 창틀 창 뒷변 배경 모서리에 있는 룩 모델 수정 할거야 트리포에게 생성 하기 좋은 모델 뭐야 그리고 모서리에 있는 룩 모델 기능이 있는 거야?" → 촛불 코드 · 모델 · 렌더 · 스크립트 지움(문서에는 안 적었던 것). 사용자가 Tripo로 룩 · 아치 창틀을 줌(`tripo_raw/rook_pedestal.glb` · `window_frame.fbx`, 이번에도 텍스처 없음).

**룩 받침의 기능(코드 확인):** 돌 충돌체(부딪히고 올라섬), 판 축소에서 안 지워짐(`SwordGhosts.Plan`이 빼고 고름), 출발 · 부활 칸 아님(`SwordBoard.StartCell` · `ChooseSpawn`). 점수 기능은 없음.

`clean_tripo_v2.py`(렌더 `v1/SF_Arena_v1_v2*.png`):

| 결과 | 내용 |
|---|---|
| `SF_Prop_Rook` | 체스 룩. 사용자 "0.9m 으로 맞추고 재질과 색상도 체스판과 맞춰" → "높이만 바꿔야지 너비감은 그대로": **폭 1.45 m(예전 받침 발 그대로) · 높이 0.9 m**로 납작하게, 칸과 같은 크기의 나무 UV, 5,000 삼각형. **게임: 흰 쪽(b2 · g2)은 밝은 오크, 검은 쪽(b7 · g7)은 월넛**(칸과 같은 텍스처 재질). 충돌은 룩 모양 3조각(발 · 몸통 · 왕관, 볼록, 모델 크기에서 잼, `RookCollider`). (처음엔 2.4 m 돌 석상이었음) |
| `SF_Prop_Window` | Tripo 아치 창틀 **그대로**(폭 28 m · 높이 29 m, 짙은 나무 `#2a1c13`, 사용자 "창틀에 너가 모델링 한 것을 빼주고"). 빈 물체 `Front`가 판 쪽. 게임: 책장 사이 z 50, 책상 위 3 m. **창 뒤 배경**(사용자 "창틀에 아까처럼 배경을 해줘"): 창틀 크기에서 잰 푸른 회색 판 한 장(빛 없이 · 안개 받음 `#6f82a6`)을 창틀 바로 뒤에, 모델과 따로(`SwordFightArena.Room`). (처음엔 유리 · 창살을 모델에 넣었다가 뺌). **달 배경**(사용자 "배경으로는 달이 좋겠지?" → "달 배경 만들어"): 그 판에 그림 `textures/T_SF_Night.png`(1024×1224, `make_night_texture.py`): 아래 푸른 회색 `#6f82a6` → 위 짙은 남색 하늘, 아치 안 보름달(크림, 옅은 바다 무늬, 테두리 쪽 어둡게) + 푸른 빛무리, 작은 별 약 90개. 빛 없이 그려 안개는 받고, 가장 밝은 점이 1이라 블룸 기준(1.2) 아래 → 스킬 이펙트보다 튀지 않음. 게임은 `Resources/SwordFightMap/T_SF_Night`을 씀(없으면 예전 단색) |

- 책장(뒷벽 배경)은 아직 코드 상자 그대로(Tripo 책장 모델을 주면 바꿈).
- 모델이 없으면 예전 받침 · 창.

## 14. 다음

- [x] 판 색: 밝은 칸 밝은 오크, 어두운 칸 보낸 월넛 텍스처(10-28)
- [x] 사용자: v1 렌더 확인 → 다듬기(10-28)
- [x] Tripo 프롬프트(§10, 10-28): 책 3종 · 시계 몸통 · 램프. 룩 받침은 Blender 것
- [x] 사용자: Tripo GLB 5개(10-28) → 정리 · 쌓기 · Unity 연결, **사용자 Unity 화면 확인(10-28)**
- [x] Blender 스크립트: 판 부품 `build_arena_v1.py`, Tripo 정리 `clean_tripo_v1.py`(10-28)
- [x] Unity 연결: `SwordFightArena.Build`가 모델을 씀(10-28, 사용자 화면 확인)
- [x] 룩 E 떨어지는 조각을 테두리 모양으로(10-28, 사용자 Unity 확인)
- [ ] Unity 경기 중 확인: 판 축소(F9) · 킹 E · 2.5 m 칸(VALIDATION R191 4 · 5 · 7 · 8)
- [x] 분위기 1~3(§11, 10-28, Unity 미확인)
