# 첫 실행 튜토리얼 맵 — 왕궁 훈련장 (R834)

컨셉: `Docs/Player/ConceptArt/R834_Tutorial/TU_1_palace_drill_yard.png`. 에셋 시트: `Docs/Player/ConceptArt/R834_Tutorial/AssetRefs/TU_assets_0N_*.png`.

**상태: Blender 맵 1차. Unity에는 아직 넣지 않았고 사람이 본 적 없음.**

## 다시 만드는 법

```
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python tu_textures_v1.py
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python tu_map_v1.py -- --inspect --render --save
```

| 파일 | 하는 일 |
|---|---|
| `tu_textures_v1.py` | 새로 만든 기하에 쓰는 이음매 없는 텍스처 6장(`Materials/Textures/TU_*`), numpy로 생성, 고정 시드 |
| `tu_kit_common.py` | Tripo GLB 읽기·나누기·검사·수리·렌더 공용 함수 |
| `tu_map_v1.py` | 부품 정리 + 새 부품 모델링 + 맵 조립. `--inspect` 부품 검사 사진, `--render` 맵 사진(Cycles), `--save` `TU_Map_v1.blend`, `--views=a,b` `--samples=N` |
| `tu_kit_split_v1.py` | `--probe`: GLB를 나눈 그대로(수리 전) 보는 사진 |
| `tu_kit_report.json` | 부품별 검사 수치(수리 전·후, 면 수, 크기) |
| `Renders/inspect_*.png` | 부품 검사 사진(앞·뒤). **뒷면을 자홍색으로 칠해서** 구멍이나 뒤집힌 면이 있으면 바로 보이게 함 |
| `Renders/map_*.png` | 맵 사진 9장 |

좌표: Blender (x, y, z) = Unity (x, z, y). 게임이 판정하는 숫자(`LabLayout`·`TutorialLayout`·`RagdollLabBuilder.BuildGeometry`)는 그대로 썼다 — 콜라이더 상자마다 같은 크기의 꾸민 메시. 콜라이더는 하나도 더하거나 바꾸지 않았다.

## 부품표

Tripo 원본(`Source/`): A1 = 시트 01, A2 = 시트 02, A3 = 시트 03. 시트 04·05는 GLB가 없어 Blender로 만듦. **A3에는 낮은 턱과 림보가 빠져 있어** 둘도 Blender로 만듦.

| 부품 | 출처 | 지금 면 수(원본 → 손질 후) | 게임 쓰임 | 판정 | 깨짐 |
|---|---|---|---|---|---|
| CastleWall | A1 | 9,424 → 2,600 | 둘레 성벽 4 m 모듈(약 90번 반복) | 줄이기 | 없음 |
| CastleCorner | A1 | 11,378 → 3,000 | 예비(모서리는 탑) | 줄이기 | 없음 |
| CastleTower | A1 | 20,868 → 9,000 | 모서리·변 가운데 탑 7개, 높이 10 m | 줄이기 | 없음 |
| CastlePilaster | A1 | 6,076 → 1,600 | 성벽 3칸마다 | 줄이기 | 없음 |
| Gatehouse | A2 | 16,674 → 9,000 | 북쪽 정문, 폭 10 m | 줄이기 | 없음(열린 가장자리 8개는 바닥 안쪽, 사진에 안 보임) |
| WallNiche | A2 | 8,052 → 2,400 | 동·서 성벽 벽감 4개 | 줄이기 | 없음(뒷면은 벽에 붙음) |
| KnightStatue | A2 | 6,208 → 4,500 | 벽감·정문 옆, 높이 2.6 m | 줄이기 | 없음 |
| BishopStatue | A2 | 4,790 → 3,500 | 벽감·정문 옆, 높이 2.6 m | 줄이기 | 없음 |
| StoneSteps | A2 | 7,971 → 2,400 | **안 씀**: 정문 앞에 둘 자리가 없음(해자 위로 뜸) | 줄이기 | 없음 |
| ClimbBlock | A3 | 9,814 → 3,000 | **안 씀**: 2·3·4 m 벽은 5 × 6 m 정확한 상자여야 함 | 줄이기 | 없음 |
| Ramp | A3 | 14,548 → 3,000 | **안 씀**: 경사로는 7 ~ 19 m | 줄이기 | 없음 |
| Tunnel | A3 | 7,488 → 2,400 | **안 씀**: 게임 터널은 천장이 평평한 0.65 m | 줄이기 | 없음 |
| 훈련 벽 2·3·4 m | Blender | 1,512 / 1,944 / 2,808 | 동쪽 벽(5 × 6 m), 금 손잡이·금 테 | 새로 | 없음 |
| 낮은 턱 | Blender | 216 | 수업 2 턱 0.3 × 0.45 × 5 m | 새로 | 없음 |
| 림보 | Blender | 8,920 | 기둥 0.3 × 0.6, 봉 4 × 0.6 × 0.25(틈 0.55 m) | 새로 | 없음 |
| 터널 | Blender | 358 | 틈 0.65 m, 길이 3 m, 위는 낮은 둥근 지붕 | 새로 | 없음 |
| 회전 봉 | Blender | 3,716 | 13 m, 높이 0.45 m, 반지름 0.2 m(돌아가는 물체의 겉모습) | 새로 | 없음 |
| 외나무다리 | Blender | 324 | 0.6 × 0.5 × 16 m 호두나무, 끝 금 죔쇠 | 새로 | 없음 |
| 졸업 문 | Blender | 6,072 | 착지대 위, 커튼은 Unity 코드 몫 | 새로 | 없음 |
| 난간·난간 기둥 | Blender | 2,372 / 1,000 | 바깥 테두리(놀이 바닥에서 10 m 이상) | 새로 | 없음 |
| 화분·측백 | 재사용 | — | `Early/Export/D_FlowerPot`·`A_Cypress` | 그대로 | — |

판정 근거: Tripo 원본은 시트 전체 31,850 ~ 47,746 삼각형으로, 반복해 쓰는 성벽·기둥은 한 개에 2,000 안팎이 알맞다. 정문·탑은 가까이서 보여 9,000까지 둔다. 원본의 열린 가장자리(시트마다 5,000 ~ 8,000개)는 UV 이음매에서 갈라진 점이었고, 0.4 cm 안의 점을 합치자 대부분 0이 됐다.

## 맵 구성

- 놀이 바닥(높이 0): 가운데 30 × 30 m는 2.5 m 체스판(크림·호두색 대리석), 회전 봉 바닥 16 × 16 m와 착지대 6 × 6 m는 2 m 체스판, 나머지는 크림 판석. 바깥 가장자리마다 금 테.
- 놀이 바닥 둘레는 **마른 해자**: 벽이 높이 0에서 −10 m까지 내려가고(6 m마다 부벽), 바닥은 −10 m. 낙사 구역 꼭대기(−8 m) 아래라 떨어진 폰이 해자 바닥에 살아서 서는 일은 없다.
- 해자 바깥 6 m 폭의 테두리 길에 난간, 화분·측백, 그 바깥에 성벽·탑·정문·벽감 조각상.
- 깃발·커튼·불빛·금빛 목표 칸은 넣지 않음(Unity 코드 몫).

## Unity

```
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b TU_Map_v1.blend --python tu_export_unity_v1.py
```

- `Assets/Art/Tutorial/Models/TU_Map.fbx`(TU_Course 13,770면 · TU_Castle 376,517면 · TU_Grounds)와 `TU_Sweeper.fbx`, 텍스처 `Assets/Art/Tutorial/Textures`.
- `RagdollLabBuilder.TutorialDress.cs`: 튜토리얼 씬을 지을 때(`Rebuild Tutorial Scene`) 회색 상자는 콜라이더를 남기고 그리기만 끔, 맵 FBX를 원점에, 회전 봉 겉모습을 `Spinning Bar` 아래에. 재질 `Assets/Art/Tutorial/Materials/TU_*.mat`(URP Lit)을 만들어 FBX에 다시 연결. 화분·측백은 `Resources/PawnRushMiniGames/Materials`의 기존 재질. 위치 점검 로그 "[Tutorial] 맵 겉모습 위치 맞음". `-tutorialNoDress` 인자로 회색 상자 그대로 지을 수 있음.
- 사본 검사(`Tools/Run-Tutorial.ps1`, 11-25): 위치 맞음, 수업 1 ~ 6 · 10 통과, Play 오류 0. 수업 7 봇 검사(B2 · L7-2 · B10)는 다른 세션 작업 — 맵을 얹은 뒤 두 번 실패, `-tutorialNoDress`(맵 없이)로도 같은 셋이 실패 → 맵 때문이 아님.
- **사람이 Unity에서 본 적 없음.**

## 남은 일

- 시험용 레인 둘(역경사·곡면, `[3b]`)은 시험장 모양 그대로 돌 무늬만 입힘 — 튜토리얼에서 뺄지 사용자 결정.
