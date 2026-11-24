# 구간 사이 복도 CR04 ~ CR07 모델 (2026-11-22)

복도 세션 둘 중 둘째가 만든 것: 들어가는 구역에 맞춘 복도 넷. CR01 ~ CR03은 다른 세션(`cr13_common.py` · `cr13_ornaments.py`).

| 복도 | 들어가는 곳 | 높이 | 스크립트 | 모듈 |
|---|---|---|---|---|
| CR04_LowerRooms | 대계단 홀 → 아랫궁 방(B · E · F · J) | 6.5 | `CR04_LowerRooms/cr47_04_lowerrooms.py` | Bay · BayMid · Pier · CeilingBeam · CeilingBay · Portal(시작) · Header(끝) + Sconce · Portrait · Chandelier |
| CR05_KingsAntechamber | 대계단 홀 → 왕의 앞방(G · H · I) | 7.5 | `CR05_KingsAntechamber/cr47_05_kingsantechamber.py` | 같은 틀 + FloorInlay(보라 러너) + GlobeSconce |
| CR06_WarRoom | 둘째 관문 → 국왕 작전실 | 6.5 | `CR06_WarRoom/cr47_06_warroom.py` | Header(시작) · Portal(끝) · EndWall · FloorInlay(나침반) + Lantern · CeilingCompass |
| CR07_Library | 둘째 관문 → 왕의 서재 | 7.5 | `CR07_Library/cr47_07_library.py` | Header(시작) · Portal(끝) · FloorInlay(양탄자) + ReadingLamp |

- 공통 도구: `cr_kit_04_07.py`(상자 · 회전체 · 관 · 아치 벽, 재질, 미리보기, 내보내기) · `cr47_parts.py`(창 · 커튼 · 판벽 · 방패) · `cr47_ornaments.py`(장식 6종, GPT 시트 `Docs/PawnRush/ConceptArt/SectionCorridors/AssetRefs/CR_ornaments_01.png`를 Blender로 직접) · `cr47_run.py`(조립 · 사진 · 내보내기).
- 실행(백그라운드 Blender, 같이 쓰는 Blender는 안 씀): `"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python <스크립트> -- [--render] [--export]`
- 내보내는 곳: `Assets/Resources/PawnRushCorridors/CR0x_<이름>/CR0x_<모듈>.fbx` + `CR0x_Layout.json`, 재질 `Assets/Resources/PawnRushCorridors/Materials/PR_Cor_*.mat`(미니게임 텍스처를 guid로 씀, 있는 .mat은 다시 쓰지 않음).
- 약속(두 복도 세션 합의): 서쪽 벽 기준(x 0 = 벽 몸 면, +x가 복도 안), 동쪽은 y축 180도. 칸 · 천장 칸은 맑은 폭 1.9 m로 만들고 로더가 실제 폭에 맞춰 늘림. 기둥 0.9 m. `LIGHT_n` = 빛 + 불꽃, `FLAME_n` = 불꽃만. 장식 위치는 Layout.json(`per` pier/bay, `index`, `mirror`, `offset` [벽 면에서 안쪽, 높이, 길 방향]).
- 사진: `previews/cr47_*.png`(사람 눈높이 · 벽 · 바깥), 모아 보기 `previews/cr47_CR04-07_pawn_views.png`.
- 아직 Unity에서 본 사람 없음. 로더 코드는 첫째 복도 세션 몫.
