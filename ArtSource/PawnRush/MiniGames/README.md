# 폰 러시 미니게임 Blender 모델 (R501~)

[3단계 아트 기준](../../../Docs/PawnRush/PAWN_RUSH_ART_PHASES.md)에 따라 미니게임 구간을 Blender로 모델링한다. **판정·크기·시스템은 게임 코드 그대로**이고, 모델은 그 위에 얹는 그림이다. 충돌체·문·밧줄·손잡이·등불은 게임이 만든 것을 쓴다.

| 폴더 | 단계 | 재질 세트 |
|---|---|---|
| `Early/` | 1단계 (C·D·A) | 정원 `GC_` 세트. `gc_kit.py`를 그대로 불러 쓴다 |
| `Mid/` | 2단계 (B·E·F·J) | 대계단 홀 세트 (아직 없음) |
| `Late/` | 3단계 (G·H·I) | 새 세트 (아직 없음) |

## C · 해자 도개교 (`Early/c_drawbridge_v1.py`)

```
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python c_drawbridge_v1.py -- --render --export
```

- `--render`: `render_c_entry/overview/winch/bridge.png` (게임 부품 대역을 함께 그린다: 물, 금 테두리, 밧줄, 손잡이, 문, 폰)
- `--export`: `Assets/Resources/PawnRushMiniGames/Early/`에 FBX 3개를 내보낸다. 새 재질은 `Assets/Resources/PawnRushGardenConcept/Materials/GC_c_*.mat`로 만들고, 이미 있는 `.mat`(정원 것 포함)은 덮어쓰지 않는다

| FBX | 기준 좌표 | Unity에서 붙는 곳 |
|---|---|---|
| `C_Drawbridge_Env` | 구간(입구 0,0,0 · 출구 0,0,36) | 구간 `Content/MODEL_Env` |
| `C_Drawbridge_Deck` | 다리 힌지 (x −2~2, 길이 4.1 m, 윗면 0) | 두 팀의 `Drawbridge hinge/MODEL_Ride` — 게임이 돌리는 대로 함께 움직임 |
| `C_Drawbridge_Drum` | 드럼 축 (x 방향, 반지름 0.55 m) | 두 팀의 `Drum/MODEL_Ride` — 게임이 돌리는 대로 함께 돎 |

Unity 연결은 `Assets/Scripts/PawnRush/MiniGameModels.cs`에 있다. 게임이 미니게임을 설치할 때마다 모델을 붙이고, 모델이 대신하는 코드 그림(R312 건축 껍데기, 공용 바닥, 해자 벽, 다리 탑, 권양기 틀, 밧줄 길, 다리 상판, 드럼)은 그리기만 끈다(충돌은 그대로). FBX가 없으면 예전 코드 그림이 그대로 나온다.

지키는 것: 좌표는 `MiniGameSectionBuilder`·`MgDrawbridge`의 숫자를 그대로 옮겼다. 게임이 계속 그리는 면과는 1 cm 이상 떼었다(R498). 장식에는 팀색을 쓰지 않고 팀 깃발에만 쓴다. 기능 금(눈금)만 빛나고, 장식은 무광 황동이다. 지붕은 회색 슬레이트다.
