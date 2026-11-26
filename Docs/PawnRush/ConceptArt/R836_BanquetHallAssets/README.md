# 대연회장 모델링 에셋 시트 (R836, 2026-11-25)


**바뀜(11-25 사용자 "블렌더에서 작업 할거고 세션 2개 켜서 아주 정밀하게 작업 할거야 최대한 컨셉 아트랑 똑같이 해"):** Tripo는 쓰지 않는다. 아래 표의 '만드는 길' 열과 Tripo 면 수는 참고로만 남긴다 — 모든 부품을 Blender 스크립트로, 시트 칸과 나란히 비교하며 만든다([PLAN 2절](../../../../ArtSource/PawnRush/BanquetHall/PLAN.md)).
사용자: "모델링 들어갈 모든 에셋 뽑아". 계획 [`ArtSource/PawnRush/BanquetHall/PLAN.md`](../../../../ArtSource/PawnRush/BanquetHall/PLAN.md)의 B1 ~ B7 구역에 들어갈 것을 Codex 시트 12장(모델 11 · 재질 1)으로 뽑았다.
형식은 튜토리얼(R834) · 정비동(R503)과 같다: 흰 바탕 한 줄, 같은 3/4 시점, 글자 · 불꽃 · 빛나는 유리 · 흔들리는 천 없음(촛대는 빈 컵 — 불과 빛은 Unity 코드).
재질은 [R787 컨셉](../R787_BanquetHall/banquet_concept.webp), 배치 · 비율은 [R833 그림](../R833_BanquetHallConcept/)을 첨부로 줬다. 프롬프트 `AssetRefs/PROMPT_1 ~ 3.md`(+ 공통 `_common.md`, 실제로 보낸 글 `full_*.md`), 기록 `codex_log_*.txt`.

**Tripo 권장 면 수(시트 전체):** A01 약 45,000 · A02 약 35,000 · A03 약 35,000 · A04 약 40,000 · A05 약 45,000 · A06 약 50,000 · A07 약 40,000 · A08 약 40,000 · A09 약 60,000 · A10 약 50,000 · A11 약 50,000 (T1은 텍스처라 Tripo 아님). 시트마다 들어 있는 부품 수와 게임 속 반복 수를 보고, 손질 뒤 목표의 3 ~ 5배로 잡았다 — 줄이기는 Blender에서 되지만 늘리기는 Tripo 재출력뿐이다.

| 시트 | 구역 | 든 것 (왼쪽부터) | 만드는 길(제안) |
|---|---|---|---|
| [BH_A01_hall_walls](AssetRefs/BH_A01_hall_walls.png) | B7 홀 셸 | 아치 창 벽 칸 · 창 없는 벽 칸 · 호두나무 벽기둥 · 큰 아치 출입구 · 벽 촛대(빈 컵 셋) | Blender 스크립트(반복 모듈) |
| [BH_A02_ceiling_loft](AssetRefs/BH_A02_ceiling_loft.png) | B7 · B6 | 격자 천장 칸 · 천장 장미 장식 · 회랑 난간 · 회랑 앞 보와 까치발 · 금 사다리(고리 둘) | Blender 스크립트, 장미 장식만 Tripo |
| [BH_A03_servery_screens](AssetRefs/BH_A03_servery_screens.png) | B1 | 칸막이 판 · 배식대와 계단 · 부엌 문(반쪽 여닫이) · 식료품실 문틀 · 접시 더미 | Blender 스크립트 |
| [BH_A04_long_table](AssetRefs/BH_A04_long_table.png) | B2 | 긴 식탁 칸(식탁보 · 러너) · 접시 · 금잔 · 다섯 갈래 촛대 · 손님 의자 | 식탁 · 접시는 스크립트, 잔 · 촛대 · 의자는 Tripo |
| [BH_A05_cloche_lift](AssetRefs/BH_A05_cloche_lift.png) | B2 | 은 뚜껑 · 은 접시(금 고리 둘) · 승강 틀 기둥 · 도르래 · 금 사슬 고리 · 쇠줄 고리 | 스크립트(움직이는 판정에 딱 맞춰야 함) |
| [BH_A06_dais_balcony](AssetRefs/BH_A06_dais_balcony.png) | B3 | 왕의 단 앞면 · 뒤 계단 · 왕의 식탁 · 왕의 발코니(굳은 차양) · 왕좌 | 단 · 계단 · 식탁은 스크립트, 발코니 차양 · 왕좌는 Tripo |
| [BH_A07_stage_buffet](AssetRefs/BH_A07_stage_buffet.png) | B5 | 무대 앞면 · 무대 위 장식 띠(굳은 술) · 디저트 뷔페 · 3단 케이크 받침 · 콘트라베이스와 활 | 무대 앞면 스크립트, 나머지 Tripo |
| [BH_A08_chocolate_fountain](AssetRefs/BH_A08_chocolate_fountain.png) | B5 | 분수 전체 · 층 접시 하나 · 왕관 꼭지 | 스크립트(층 높이 · 지름이 판정이라 회전체로), 왕관만 Tripo |
| [BH_A09_chandeliers](AssetRefs/BH_A09_chandeliers.png) | B6 | 왕관 샹들리에(맨 아래 잡는 고리) · 큰 샹들리에(위가 걷는 초록 판) · 사슬 · 천장 고정쇠 | 스크립트(`BanquetChandelierShape`를 정밀하게), 수정 방울은 반복 부품 |
| [BH_A10_royals_dancers](AssetRefs/BH_A10_royals_dancers.png) | B3 · B4 | 왕 · 여왕 무희(어깨가 평평한 턱 — 서는 자리) · 짝(검은 연미복) · 손잡은 한 쌍 | Tripo |
| [BH_A11_guests_staff](AssetRefs/BH_A11_guests_staff.png) | B1 · B2 · B5 | 웨이터(흑 비숍) · 앉은 손님(뒷모습, 백 폰) · 마술사(룩 몸, 보라 망토 · 실크햇 · 지팡이) · 바이올린 악사 · 콘트라베이스 연주자(흑 룩) | Tripo |
| [BH_T1_material_swatches](AssetRefs/BH_T1_material_swatches.png) | B0 | 크림 석재 · 대리석 · 호두나무 · 금 문양 · 은 · 리넨 · 왕관 무늬 올리브 러너 · 올리브 벨벳 · 붉은 벨벳 · 체스판 바닥 · 초콜릿 · 상아 | 잘라서 이음매 없는 타일로(B0 재질 라이브러리) |

**다른 채팅 것을 그대로 쓸 수 있는 것(시트에 넣지 않음):** 악보대 `F_MusicStand`, 하프 `F_Harp`(무대 장식으로 쓸 때), 화분 `D_FlowerPot` · `F_Topiary`(꽃 장식). 춤추는 인형 `F_DancerGown` · `F_DancerTailcoat`은 태엽 인형이라 여기 기물과 결이 달라 쓰지 않는다.

**볼 것 — 판정과 다른 곳:**
- A10 여왕 무희의 어깨 턱은 그림이 좁다. 게임은 어깨 7.4 m 높이에 폰이 서는 판이므로 모델은 판정 크기에 맞춰 넓힌다.
- A05 은 뚜껑은 위가 둥근 종 모양이다(회색 상자는 지름 4.4 m · 띠 1.6 m). 모양은 그림대로, 크기는 판정대로.
- A08 분수는 네 층으로 그려졌다(게임은 그릇 + 세 층). 층 수 · 높이는 빌더 수치대로.
- A09 큰 샹들리에는 위의 S자 팔이 판 위에 서 있다 — 게임에서 판 위는 걷는 곳이라 팔이 발에 걸리지 않게 가운데 기둥으로 모은다.
- A11 손님은 의자 없이 앉은 모양이다. 의자는 A04의 것을 같이 쓴다.
