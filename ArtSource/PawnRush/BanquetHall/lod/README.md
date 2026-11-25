# 대연회장 경량화 · LOD (bh-hall, R838, 2026-11-25)

사용자 "다른채팅에서 작업 한것도 사진으로 비교 하고 모양이 변하지 않는 곳까지 경량화 작업을 한 후 대연실 LOD 할 곳 있으면 해".
도구: [`bh_lod.py`](../bh_lod.py) (모듈 설명에 방법), 전체 돌리기 [`run_all.sh`](run_all.sh).

- **LOD0 (가까이)**: 같은 평면의 면을 합친 뒤 모서리 접기(edge collapse)로, 원본 표면에서 **2 mm**(작은 물체는 대각선의 0.3 %)를
  넘지 않는 가장 낮은 비율. 모든 점을 검사하고 한 점도 3 mm를 넘지 않음 — 작은 돋을새김도 남는다. 25 %도 못 줄이면 **원본 그대로**
  (원래 음영 유지: 큰 샹들리에 왕관 띠의 네모 장식이 새 음영에서 흐려져서).
- **LOD1 / LOD2 (멀리)**: 앞 단계에서 12 mm / 40 mm까지, 삼각형이 앞 단계의 2/3 이하일 때만 둔다.
- 음영: 30° 넘게 꺾인 모서리는 날카롭게 + 면적 가중 법선(원본의 bevel harden과 같게). 줄인 메시에 원본 법선을 옮기면 넓은 면에
  빛이 번져서(카운터 계단 · 식탁 윗면) 쓰지 않음.
- bh-path의 FBX(`Assets/Resources/BanquetHall/Models/B1 ~ B4`, 23개 — `bh_chain_link`는 작아서 뺌)는 메시 이름 `<이름>_LOD0/1/2`로
  다시 씀 → Unity 가져오기가 LODGroup을 만든다(이름 규칙). 원본은 `lod/orig/<구역>/`(git 밖). 재질 이름은 그대로라 입히기 코드
  (`GrandBanquetHallDressing.Paint`)는 그대로 동작. **Unity에서 아직 확인 안 함**(LODGroup이 생기는지 · 전환 거리).
- bh-hall 부품(B5 ~ B7)은 [`bh_hall_export.py`](../bh_hall_export.py)가 조립대로 지어 같은 방법으로 단계를 붙여 `Models/Hall` · `Chandeliers` · `Stage`에
  FBX 17개로 씀(`lod/parts/*.blend`는 비교용). Unity에서는 `GrandBanquetHallDressing.Hall.cs`의 `SplitLods`가 FBX 하나에 하나뿐인
  LODGroup을 지우고 **부품마다** LODGroup을 만든다(화면 높이 16 % · 5 %, 두 단계면 10 %에서 바뀜) — bh-path 모델에도.
- bh-path가 모델을 다시 내보내면: `lod/orig/<구역>/<모델>.fbx`를 지우고
  `blender -b --factory-startup --python bh_lod.py -- --fbx <구역>/<모델> --write`.

사진 `stills/lod/<이름>.png`: 원본 | LOD0 | LOD1 | LOD2 (같은 카메라), 위 전체 · 아래 가까이. 숫자 `stills/lod/report.json`.

| 모델 | 원본 삼각형 | 둔 단계 (LOD0 / LOD1 / LOD2) | LOD0 감소 | 사진 |
|---|---|---|---|---|
| B1_bh_counter | 31,644 | 20,996 / 4,010 | 34% | [사진](../stills/lod/B1_bh_counter.png) |
| B1_bh_kitchen | 8,348 | 8,348 / 804 | 0% | [사진](../stills/lod/B1_bh_kitchen.png) |
| B1_bh_kitchen_leaf_E | 3,192 | 2,116 / 262 / 94 | 34% | [사진](../stills/lod/B1_bh_kitchen_leaf_E.png) |
| B1_bh_kitchen_leaf_W | 3,192 | 2,116 / 252 / 94 | 34% | [사진](../stills/lod/B1_bh_kitchen_leaf_W.png) |
| B1_bh_pantry | 15,904 | 10,186 / 936 | 36% | [사진](../stills/lod/B1_bh_pantry.png) |
| B1_bh_screens | 148,092 | 148,092 / 13,304 / 6,398 | 0% | [사진](../stills/lod/B1_bh_screens.png) |
| B1_bh_waiter | 20,806 | 20,806 / 11,448 / 2,821 | 0% | [사진](../stills/lod/B1_bh_waiter.png) |
| B2_bh_cloche | 12,480 | 8,080 / 2,212 / 712 | 35% | [사진](../stills/lod/B2_bh_cloche.png) |
| B2_bh_dumbwaiter | 78,224 | 78,224 / 8,526 | 0% | [사진](../stills/lod/B2_bh_dumbwaiter.png) |
| B2_bh_guests | 68,841 | 68,841 / 29,907 / 10,719 | 0% | [사진](../stills/lod/B2_bh_guests.png) |
| B2_bh_magician | 13,322 | 13,322 / 7,434 / 4,914 | 0% | [사진](../stills/lod/B2_bh_magician.png) |
| B2_bh_platter | 4,680 | 3,000 / 820 / 352 | 36% | [사진](../stills/lod/B2_bh_platter.png) |
| B2_bh_table_body | 31,056 | 31,056 / 3,396 | 0% | [사진](../stills/lod/B2_bh_table_body.png) |
| B2_bh_table_chairs | 156,600 | 29,034 / 14,596 / 6,522 | 81% | [사진](../stills/lod/B2_bh_table_chairs.png) |
| B2_bh_table_cloth | 126,803 | 82,606 / 40,127 / 20,112 | 35% | [사진](../stills/lod/B2_bh_table_cloth.png) |
| B2_bh_table_ware | 176,320 | 108,110 / 31,406 / 10,408 | 39% | [사진](../stills/lod/B2_bh_table_ware.png) |
| B3_bh_balcony | 74,788 | 74,788 / 9,070 / 3,495 | 0% | [사진](../stills/lod/B3_bh_balcony.png) |
| B3_bh_dais | 132,872 | 132,872 / 17,396 / 5,806 | 0% | [사진](../stills/lod/B3_bh_dais.png) |
| B3_bh_dais_rails | 123,220 | 123,220 / 14,954 / 7,282 | 0% | [사진](../stills/lod/B3_bh_dais_rails.png) |
| B3_bh_head_tables | 156,532 | 134,620 / 54,210 / 16,031 | 14% | [사진](../stills/lod/B3_bh_head_tables.png) |
| B3_bh_thrones | 55,032 | 11,424 / 5,472 / 2,228 | 79% | [사진](../stills/lod/B3_bh_thrones.png) |
| B4_bh_couple | 28,451 | 22,035 / 7,985 / 3,714 | 23% | [사진](../stills/lod/B4_bh_couple.png) |
| B4_bh_dance_floor | 10,208 | 10,208 / 932 | 0% | [사진](../stills/lod/B4_bh_dance_floor.png) |
| b5_bass | 38,100 | 38,100 / 2,069 / 755 | 0% | [사진](../stills/lod/b5_bass.png) |
| b5_buffet | 128,536 | 52,576 / 19,902 / 11,601 | 59% | [사진](../stills/lod/b5_buffet.png) |
| b5_cake | 38,144 | 22,558 / 6,066 / 1,953 | 41% | [사진](../stills/lod/b5_cake.png) |
| b5_fountain | 42,132 | 42,132 / 11,593 / 5,159 | 0% | [사진](../stills/lod/b5_fountain.png) |
| b5_pelmet | 44,828 | 16,004 / 4,708 | 64% | [사진](../stills/lod/b5_pelmet.png) |
| b5_pleat | 52,008 | 30,124 / 5,756 / 1,787 | 42% | [사진](../stills/lod/b5_pleat.png) |
| b5_stage_front | 9,088 | 9,088 / 5,393 / 2,005 | 0% | [사진](../stills/lod/b5_stage_front.png) |
| b6c_great | 95,696 | 95,696 / 26,458 / 8,672 | 0% | [사진](../stills/lod/b6c_great.png) |
| b6c_hanging | 39,968 | 23,148 / 7,588 / 2,486 | 42% | [사진](../stills/lod/b6c_hanging.png) |
| b6l_loft_face | 34,012 | 9,322 / 2,894 / 1,460 | 73% | [사진](../stills/lod/b6l_loft_face.png) |
| b6l_loft_ladder | 3,764 | 3,764 / 1,180 / 334 | 0% | [사진](../stills/lod/b6l_loft_ladder.png) |
| b6l_loft_rail | 11,720 | 6,114 / 2,192 / 1,324 | 48% | [사진](../stills/lod/b6l_loft_rail.png) |
| b7_arch_doorway | 21,016 | 5,858 / 3,120 / 1,722 | 72% | [사진](../stills/lod/b7_arch_doorway.png) |
| b7_arch_tripo | 54,193 | 44,983 / 13,979 / 9,114 | 17% | [사진](../stills/lod/b7_arch_tripo.png) |
| b7_banner | 26,344 | 4,468 / 2,651 / 712 | 83% | [사진](../stills/lod/b7_banner.png) |
| b7_coffer | 4,428 | 4,428 / 492 | 0% | [사진](../stills/lod/b7_coffer.png) |
| b7_fireplace | 25,268 | 9,988 / 6,104 | 60% | [사진](../stills/lod/b7_fireplace.png) |
| b7_panel_bay | 4,722 | 4,722 / 2,027 | 0% | [사진](../stills/lod/b7_panel_bay.png) |
| b7_pilaster | 24,632 | 19,722 / 4,741 / 2,999 | 20% | [사진](../stills/lod/b7_pilaster.png) |
| b7_rosette | 41,536 | 8,060 / 2,288 / 494 | 81% | [사진](../stills/lod/b7_rosette.png) |
| b7_sconce | 12,820 | 4,210 / 1,112 / 304 | 67% | [사진](../stills/lod/b7_sconce.png) |
| b7_window_bay | 6,772 | 6,772 / 1,921 | 0% | [사진](../stills/lod/b7_window_bay.png) |

합계 — bh-path FBX 23개: 원본 1,480,607 → LOD0 1,144,100 → 가장 먼 단계 120,306.
bh-hall 부품 22개(한 개씩, 조립에서 여러 번 놓임): 원본 759,727 → LOD0 461,837 → 가장 먼 단계 68,133.
