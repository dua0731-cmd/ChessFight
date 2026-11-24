# 미니게임 공용 모델

## 승격 발판 (`promotion_pad_v2.py`, 2026-11-22)

궁전 양식 미니게임 10개 방에 같은 발판 한 벌. 디자인은 코덱스 3안 중 2번 '눌림판'(사용자 "2번으로 해서 정밀하게 재질 똑같이 블렌더에서 작업해"), 컨셉 [`pad_design_02.png`](../../../../Docs/PawnRush/ConceptArt/PromotionPad/pad_design_02.png). v1(왕관 메달)은 사용자가 버려 지웠다.

```
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python promotion_pad_v2.py -- --bake --render --export --save
```

- `--bake`: 절차 노드로 텍스처 3장을 구워 `Assets/Resources/PawnRushMiniGames/Common/T_PromoPad_Gold|Stone|Marble.png`(브러시 금 · 결 있는 짙은 돌 · 크림 대리석, 1024², 발판 전체에 한 번 덮임)
- `--render`: `render_promotion_pad_v2.png`(컨셉과 같은 각도, 서쪽 램프 6개 켜짐) · `_top.png`
- `--export`: `Assets/Resources/PawnRushMiniGames/Common/PR_PromotionPad.fbx`(+ `.meta`, 처음 한 번)
- `--save`: `PromotionPad_v2.blend`

| 오브젝트 | 게임이 입히는 재질 (`StationPromotion.DressModel`) |
|---|---|
| `PR_PromoPad_Plate` | 브러시 금 텍스처(금속 0.9) — 쓰면 어둡게 |
| `PR_PromoPad_Frame` | 짙은 돌 텍스처 |
| `PR_PromoPad_Corners` | 크림 대리석 텍스처 |
| `PR_PromoPad_Rivets` | 금 |
| `PR_PromoPad_Recess` | 짙은 홈 `#1D1B1A` |
| `PR_PromoPad_Base` | 방 팀색을 흰색 쪽으로 45% 옅게(컨셉의 하늘색 띠) |
| `PR_PromoPad_Lamp_00`~`23` | 틀 위 금 징, 북서쪽부터 시계 방향. 2초 동안 하나씩 켜짐(`#FFC94A` 빛) |

지킬 것: 충돌체 없음(무대가 폰을 받친다), 판 5.6 cm · 리벳 꼭대기 7.4 cm, 아랫면은 무대 위 2 mm. 판정 칸 2×2 m는 코드(`StationPromotion.Half`). Blender +y = Unity 북쪽, 다른 미니게임 모델처럼 y 180° 피벗 아래에 붙는다. 삼각형 6,332.
