# 구간 사이 복도 모델 CR01 · CR02 · CR03 (2026-11-22)

승규 님 결정(11-22): 복도는 다른 방 모델을 빌리지 않고 **복도만의 모양**으로 새로 만들고, **들어가는 구역의 컨셉**을 따른다(첫 복도만 떠나는 정원 · 마차뜰 기준). 이 폴더의 CR01 ~ CR03은 이 세션, CR04 ~ CR07은 "복도 모델링 04 · 05 · 06 · 07" 세션 몫(`cr_kit_04_07.py`).

| 키트 | 어디 | 컨셉 | 삼각형(복도 하나) |
|---|---|---|---|
| CR01_Garden | 왕실 수로 정원 → 첫째 관문(C · D · A) | `Docs/PawnRush/ConceptArt/SectionCorridors/01_garden.png` | 약 99,600 (11-22 디테일 뒤) |
| CR02_CarriageYard | 왕실 마차뜰 → 첫째 관문 | `02_carriageyard.png` | 약 56,700 (디테일 뒤) |
| CR03_HallPorch | 첫째 관문 → 대계단 현관홀 | `03_hallporch.png` | 약 69,800 (디테일 뒤) |

장식(GPT 시트 `AssetRefs/CR0x_assets_*.png`)도 Tripo 없이 전부 Blender로 만들었다(승규 님 11-22 "에셋 시트도 너가 블렌더에서 아주 정밀하게"). 디테일(승규 님 11-22 "꽃 이라던가 디테일"): 겹장미 · 봉오리 · 작은 흰 꽃 · 넝쿨 · 벽감 위 장미 꽃줄 · 수련(01), 창 꽃상자 · 담쟁이 · 밧줄 · 양동이 · 화살 구멍(02), 금 꽃줄 · 수국 화분 · 촛대 초(03) — `cr13_details.py`.

## 만드는 법

```
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python CR01_Garden/cr01_garden.py -- --render --export
```

- `--render`: 조립한 복도를 `CR0x_*/previews/`에 찍는다(게임 카메라 높이 · 벽 근접 · 반대쪽 · 바깥).
- `--export`: `Assets/Resources/PawnRushCorridors/<키트>/CR0x_<모듈>.fbx` + `CR0x_Layout.json`. 이미 있는 `.mat`은 덮어쓰지 않는다. 새 재질은 `PawnRushCorridors/Materials/PR_Cor_*`(정원 헤링본 바닥 · 홀 금 · 장식 대리석 · 벽감 파랑 · 젖빛 유리).
- 공용 도구: `cr13_common.py`(재질은 Unity `.mat`을 읽어 미리보기 색 · 텍스처를 맞춤, 미터 UV, 내보내기), `cr13_ornaments.py`(백합 문장 · 조개 분수 꼭지 · 항아리 장식 · 난간동자 · 장미 · 회양목 · 화분).

## 모듈 약속(두 세션 공통)

- Blender X = 복도 x, Y = 복도 z(나아가는 쪽), Z = 위. 벽 모듈은 **서쪽 벽**(x < 0)으로 만들고 기준점은 몸 면(x −6). 동쪽은 180° 돌림.
- 벽 칸은 1.9 m 칸으로 만들고 Unity가 실제 칸(1.867 / 1.917 m)에 맞춰 늘이거나 줄인다.
- 걷는 폭(|x| < 6) 안으로는 아무것도 나오지 않는다. 충돌은 모델에 없고 `SectionCorridor`의 상자 그대로(프로토콜 그대로).
- 표식: `LIGHT_*` 빛 + 불꽃, `FLAME_*` 불꽃만, `BANNER` 코드 깃발.

## Unity 연결

`Assets/Scripts/PawnRush/MiniGamePalaceArt.CorridorModels.cs` — `CorridorKitId`(어느 키트), `LoadCorridorKit`(레이아웃 · 모델), 모델이 있는 부분만 코드 그림을 건너뜀(`MiniGamePalaceArt.Corridor`). FBX가 없으면 예전 그림 그대로.

## 상태

Unity 밖 전체 컴파일 PASS, Blender 렌더로만 확인. **Unity에서 아무도 보지 않았다 — 사용자 확인 전.**
