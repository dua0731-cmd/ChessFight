# 폰 러시 미니게임 Blender 모델 (R501~)

[3단계 아트 기준](../../../Docs/PawnRush/PAWN_RUSH_ART_PHASES.md)에 따라 미니게임 구간을 Blender로 모델링한다. **판정·크기·시스템은 게임 코드 그대로**이고, 모델은 그 위에 얹는 그림이다. 충돌체·문·밧줄·손잡이·등불은 게임이 만든 것을 쓴다.

| 폴더 | 단계 | 재질 세트 |
|---|---|---|
| `Early/` | 1단계 (C·D·A) | 정원 `GC_` 세트. `gc_kit.py`를 그대로 불러 쓴다 |
| `Mid/` | 2단계 (B·E·F·J) | 대계단 홀 세트 (아직 없음) |
| `Late/` | 3단계 (G·H·I) | 새 세트 (아직 없음) |

**R502:** 사용자가 R501 모델이 "원하는 스타일이 아니다"라고 했다. [참고 사진 3장](../../../Docs/PawnRush/ConceptArt/R502_DrawbridgeSheets/)과 같은 재질·분위기로 다시 만든다. 첫 단계인 재질 견본 4종은 [`Early/Materials`](Early/Materials/README.md)에 있다. 도개교에는 장식 금을 쓰지 않는다.

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

## D · 정문 쇠살문 누각 (R505)

사용자가 준 AI 모델을 게임 숫자에 맞춰 손질했다. 원본·검사·손질 기록은 [`Early/Source/README.md`](Early/Source/README.md).

| FBX (`Early/Export/`) | 만드는 스크립트 | 기준 좌표 | Unity에서 붙는 곳 (`MiniGameModels.Capstan`) |
|---|---|---|---|
| `D_Capstan_Static` | `d_capstan_v1.py` | 드럼 축 바닥(지름 1.4 m, 윗면 1.8 m) | 두 팀의 `Capstan drum` 자리 |
| `D_Capstan_Bars` | `d_capstan_v1.py` | 회전축 원점(막대 0.55~3.0 m, 높이 0.40~0.62 m) | 두 팀의 `Capstan bars` — 게임이 돌리는 대로 함께 돎 |
| `D_CenterGate` | `d_center_gate_v1.py` | 문짝 앞면·바닥·가운데(19.85배, 폭 19.9 m·높이 12.2 m) | 구간 (0, 0, 23.69), 가운데 막힌 구역 앞 |
| `D_TeamArch` | `d_team_gate_v1.py` | 문 구멍 바닥 가운데(구멍 4.5 m, 기둥 0.9 m, 폭 6.3 m·높이 7.25 m) | 구간 (±12, 0, 23.8), 예전 `Team portal front` 자리 |
| `D_Portcullis` | `d_team_gate_v1.py` | 뾰족 끝 높이·가운데(폭 4.4 m·높이 4.78 m) | 두 팀의 `Team gate` 바닥 — 게임이 올리는 대로 함께 오름 |
| `D_Cypress` | `d_cypress_v1.py`(Blender로 직접) | 화분 바닥 가운데(폭 1.33 m·높이 4.75 m) | `MiniGameModels.Cypresses`, 스테이션마다 3그루(셸의 옛 나무는 숨김) |
| `D_CornerTower` | `d_tower_v1.py` | 받침 바닥 가운데(높이 13.5 m, 받침 반지름 2 m) | `MiniGameModels.Court`, (±23, 0, 25), 창이 마당 쪽(±37°) |
| `D_Wall`·`D_Lantern`·`D_GatePost`·`D_BarRack`·`D_ChainPile`·`D_HelmetBench`·`D_FlowerPot`·`D_PulleyFrame`·`D_PulleyWheel`·`D_ChainStraight` | `d_props_v1.py` | 바닥 가운데(바퀴는 축) | `MiniGameModels.Court`가 구간에 배치(벽은 빈틈 없이 이음), 바퀴·사슬은 `MiniGamePulleyChain`으로 캡스턴과 함께 돌며 맞물림. 좌표 표는 `Early/Source/README.md` |

내보낸 뒤 `Early/d_install.py`(Blender Python: `"C:/Program Files/Blender Foundation/Blender 5.2/5.2/python/bin/python.exe" -I d_install.py`)가 FBX를 `Assets/Resources/PawnRushMiniGames/Early/D/`, 텍스처를 `Assets/Art/PawnRushMiniGames/Early/Textures/`, 재질을 `Assets/Resources/PawnRushMiniGames/Materials/`(URP Lit, 베이스+노멀)에 .meta와 함께 넣는다. 게임 드럼·뚜껑·막대 그림과 가운데 구역 앞면 그림(x −9.7~9.7)은 그리기만 끈다.

**남은 D 부품의 불빛·천(사용자 11-07, B 동관 세션 전달):** 컨셉 시트의 벽 등불은 유리·불 없는 빈 틀만 에셋으로 받고 불꽃·빛나는 유리·Light는 코드로 붙인다. 팀 깃발·크림 깃발처럼 걸려 흔들리는 천은 코드(천 메시·셰이더)로 만든다. 지금까지의 D 모델(캡스턴·중앙 성문·팀 아치·쇠살문·돌 단)에는 해당 부분이 없다. 에셋 요청은 흰 배경 묶음 시트 방식(`Docs/PawnRush/ConceptArt/R503_RepairWing/AssetRefs/PROMPT_03_hall_unlit.md`).

**D 배치 때 지킬 것(사용자 11-07, 행차 회랑 세션 전달):** 기존 셸(`MiniGamePalaceArt.Shell`)의 벽·기둥·창 그림은 숨기고 모델로 다시 세팅해도 된다. **벽은 이어지게:** `D_Wall`(4.5 m) 모듈을 띄엄띄엄 두지 말고 빈틈 없이(살짝 겹치게) 벽 전체 길이를 덮고, 모듈 위로 남는 높이도 같은 재질로 채운다. 콜라이더·문·게임 장치, 구간 입구·출구 크기·위치는 그대로.

## F · 대무도회장 (R508)

작업 순서는 `Mid/PROMPT_GPT_F_J.md`(B 동관 방식). 1단계 컨셉 시트 3장 `Docs/PawnRush/ConceptArt/R508_ClockworkBallroom/`(사용자 확인 11-08). 2단계 재질 견본(사용자 확인 11-08) `Mid/Materials/r508_material_swatches.py`(새 3종: 쪽마루·자두색 벨벳·회색 거울, 나머지는 B 것, 크림 대리석은 F 전용 `PR_Mid_BallroomMarble`, 상세는 `Mid/Materials/README.md`).

게임 좌표(`MgEnPassant`·`EnPassantRules`, 스테이션 프레임 x = v·z = u, 원점 구간 (±12, 0, 4), 흑 팀은 v 거울):

| 물체 | 위치·크기 |
|---|---|
| 무용수 16개 `Statue 1~16` | 열 v −6.75·−2.25·2.25·6.75, 행 u 6·10.4·14.8·19.2(남쪽 줄부터). 높이 0.9 m, 반지름 0.38 m, 상대 팀색 |
| 레인 칸 `Lane i square s` | 칸 1.1 m, u = 집 − 0·1.1·2.2(입구 쪽으로 3칸), 높이 0.04 m. 스텝 2.2 m |
| 잡힌 폰 받침 `Tray` | v 9.2(통로 쪽, 구간 x ∓2.8), u 3~19, 폭 1.4 m, 높이 0.3 m, 양옆 금 테두리. 잡힌 폰은 u 3.4부터 1 m 간격으로 눕힘 |
| 바닥 `Station floor` | u 0~20(구간 z 4~24), 팀 문 4 × 4.6 m(u 23~23.4) |

### F 3단계 에셋 (R508, 11-08)

- 시트 3장은 `Docs/PawnRush/ConceptArt/R508_ClockworkBallroom/AssetRefs/`, 다른 방에서 가져다 쓰는 목록은 그 위 `REUSE.md`. 거울 면은 Tripo로 만들지 않고 빈 틀만 받는다(사용자 선택 "방만 비침": Blender 판 + Unity 거울 재질·반사 프로브).
- **무용수는 Blender로 직접**(사용자 11-08 "첫번 째는 너가 만들어주고 인형 느낌나게 해줘", `Mid/f_dancers_v1.py` → `Export/F_DancerGown`·`F_DancerTailcoat`·`F_WindKey.fbx`, 렌더 `Mid/render_f_dancers_front/back.png`). 인형 느낌: 도자기 몸, 어깨·팔꿈치 구체 관절, 둥근 손, 볼 홍조, 주름 12개 치마 + 레이스 밑단, 레이스 깃, 핀으로 고정한 이중 스웨그 6개(황동 구슬), 허리 띠·등 리본, 연미복은 꼬리 2개·셔츠 판·라펠·나비넥타이·황동 단추. 높이 0.895 m, 반지름 0.371·0.355 m(게임 판정 0.38 안). 재질 칸: `F_DancerBody`·`F_DancerCostume`(흰 천, Unity에서 상대 팀색)·`F_DancerTrim`·`PR_Mid_MatteBrass`·`F_DancerBlush`, 천은 `PR_Mid_Canvas` 노멀. 앞 = Blender −y, 원점 = 바닥 가운데. 태엽 열쇠는 따로(원점 = 등 구멍 (0, 0.178, 0.515)에 꽂히는 축 끝, 축 +y = 뒤쪽, 돌릴 수 있게). 삼각형: 드레스 4,000·연미복 3,500(만든 뒤 데시메이트)·열쇠 868. Unity 미반영.
- **Tripo FBX 2개**(사용자 11-08): `Mid/Source/R508_F_Frames.fbx`(+`R508_F_Frames_tex/`), `R508_F_Props.fbx`(+`R508_F_Props_tex/`), 텍스처 basecolor·normal·metallic·roughness. 검사 `Mid/f_inspect_v1.py`(렌더 `render_f_inspect_*`): 둘 다 메시 1개·0.98 m 상자, 시트 순서대로 x로 깔끔히 나뉨. 거울 틀 두 개는 가운데가 실제로 뚫려 있음.

| 부품 | Tripo 원본 삼각형 | 게임에서 | 손질 뒤 목표 | 판정 |
|---|---|---|---|---|
| 거울 팀 문짝 틀 | 82,158 | 팀마다 1개, 열릴 때 가까이 | 6,000 | 줄이기 |
| 벽 거울 틀 | 81,108 | 약 10개, 3 m 위 | 3,000 | 줄이기 |
| 스포트라이트 | 115,048 | 약 8개, 천장 | 2,500 | 줄이기 |
| 악보대 | 36,908 | 회랑·벽가 2~4개 | 2,000 | 줄이기 |
| 코트 걸이 | 27,538 | 입구 구석 2개 | 1,500 | 줄이기 |
| 음료 탁자 | 76,008 | 벽가 2~4개 | 2,500 | 줄이기 |
| 화분 토피어리 | 76,434 | 벽가·입구 4~6개 | 3,000 | 줄이기 |
| 하프 | 60,162(조각 3) | 회랑 1~2개 | 3,000 | 줄이기 |

늘릴 부품은 없다.

- **손질**(사용자 11-08 "확인, 부품 손질 진행", `Mid/f_props_v1.py` → `Export/F_<이름>.fbx`, 렌더 `Mid/render_f_props_row/door.png`): 시트 줄을 x로 나눠 부품마다 게임 크기·목표 면 수로 맞춤. 거울 문짝 3.85 × 4.55 × 0.32 m(게임 문짝, 원점 바닥 가운데) 6,000, 벽 거울 1.77 × 2.6 m·두께 0.16(원점 뒷면 바닥 가운데, 벽에 검) 3,000, 스포트라이트 높이 0.95 m(원점 천장 판) 2,500, 악보대 1.15 m 2,000, 코트 걸이 1.8 m 1,500, 음료 탁자 0.98 m 2,500, 화분 1.6 m 3,000, 하프 1.8 m 3,000. 앞 = −y. 재질은 세트마다 하나 `PR_Mid_FramesKit`·`PR_Mid_PropsKit`(Tripo 베이스 컬러·노멀을 2048로 `Mid/Materials/Textures`). **거울 판 `MIRROR_Surface`**(거울 틀의 자식, 재질 `PR_Mid_Mirror`): 틀을 정면에서 1 cm 격자로 칠하고 가운데 빈 칸을 채워 구멍 모양을 찾은 뒤 2 cm 넓혀 가장자리를 틀 밑에 숨김, 구멍 둘레 틀 깊이의 중간에 둠. 처음엔 가운데서 광선을 쏘는 방식이라 문짝 판 가장자리가 들쭉날쭉하고 벽 거울 판이 별 모양으로 깨져 바꿈. Unity 미반영.

- **Unity 배치**(사용자 11-08 "확인, Unity 배치 진행"): 방 껍데기는 E 것을 책장만 빼고 다시 내보냄(`e_hall_v1.py --ballroom` → `Export/F_HallEnv.fbx`, 옵션 없이 돌리면 E 결과 그대로). 잡힌 무용수 받침은 `f_bench_v1.py` → `F_Bench`(호두나무 받침·누빈 자두색 벨벳·황동 난간·양끝 쿠션, 16 × 1.4 m, 윗면 0.3 m = 게임 받침 윗면, 2,312 삼각형). `f_install_unity.py`로 FBX 13개 → `Assets/Resources/PawnRushMiniGames/Mid/F`, 텍스처 → `Assets/Art/PawnRushMiniGames/Mid/Textures`, 재질 10개(`PR_Mid_FramesKit`·`PropsKit`·`Parquet`·`PlumVelvet`·`BallroomMarble`·`Mirror`, `F_DancerBody`·`Costume`·`Trim`·`Blush`). 연결 `Assets/Scripts/PawnRush/MiniGameModels.Ballroom.cs`(`MiniGameModels.Apply`에 한 줄): 셸 끔 + `F_HallEnv`, E 샹들리에·벽등(`E_LightsEnv`, 불빛 코드), E 바닥(`LibraryFloors`), E 팀 문틀, 코드 팀 깃발; 무용수는 각 `Statue N/Look`에 인형(4×4 바둑판으로 드레스·연미복), 게임 몸·R312 의상은 그리기만 끄고 팀 띠 `Ring`·잡기 고리선은 둠, `MiniGameDancerLook`이 의상 칸을 상대 팀색으로(잡을 수 있는 때 금빛 쪽으로) 칠하고 등 태엽을 돌림(평소 90°/s, 창 열림 540°/s); 레인 칸 쪽마루, 받침(`Tray`·금 테두리) 대신 `F_Bench`, R312 작은 벤치 끔; 팀 문은 `DEC_PalaceGate`에 `F_MirrorDoor`(게임 문과 함께 움직임); 벽 거울 8개(회랑 창 사이 z 5·10·15·20, 아래 7.3 m), 스포트라이트 8개, 하프·악보대(회랑), 코트 걸이·음료 탁자·화분(입구 구석·뒤 복도); `MiniGameMirrorProbe`가 방 가운데 상자 투영 반사 프로브(44 × 13 × 36 m)를 Play 첫 프레임에 무용수·폰을 숨기고 한 번 그림. 컴파일·Core·모의 세션 55 통과. 사용자 확인(11-08 "확인").

- **거울 문 실시간 반사·다음 마무리**(사용자 11-08 스크린샷 + "여기는 실제로 반사 되게 유리 넣고 다음 작업 진행 해"): 거울에 기본 하늘이 비친 원인은 프로젝트 품질 설정에서 실시간 반사 프로브가 꺼져 있어서(`ProjectSettings/QualitySettings.asset` `realtimeReflectionProbes: 0`, 바꾸지 않음). 거울 문은 `MiniGameMirrorGlass`: 인트로 바닥 광택(`IntroStage.Gloss`)과 같은 방식으로 거울 뒤에 반사 카메라를 두고(시선을 거울면으로 뒤집음, 거울면을 가까운 면으로 하는 비스듬한 투영이라 문 뒤 통로는 안 찍힘) 화면 절반 크기 텍스처에 그려 새 셰이더 `Assets/Resources/PawnRushMiniGames/Fx/PR_MirrorGlass.shader`(화면 위치로 좌우 뒤집어 읽음, 은빛 색조·비스듬할수록 강해지는 유리 광택·대각선 광택 줄)로 입힘. 방의 모든 것(폰·무용수 포함)이 비침, 거울이 화면에 보이고 60 m 안일 때만 그림, 문이 움직이면 따라감. 벽 거울은 `MiniGameMirrorProbe`를 Custom 프로브로 바꿈: Play 첫 프레임에 무용수·폰을 숨기고 방 가운데에서 큐브맵(256)을 한 번 찍어 붙임(실시간 설정과 무관). 다음 작업: 스텝을 밟아 잡을 수 있는 무용수만 위에서 비추는 따뜻한 스포트 빛(`MiniGameBallroomSpots`, 팀마다 최대 3개, 8 m 위·남쪽 2.5 m에서, 켜지고 꺼질 때 서서히), 간판은 B의 호두나무·황동 틀(`DressRepairWingSigns`). 컴파일·Core 162·모의 세션 55 통과. 사용자 확인(11-08 "확인").

- **창가 휘장·무대 테두리**(사용자 11-08 "확인 다음 작업"; 앞 두 단계는 VALIDATION에 사용자 확인으로 기록): 작업 지시서의 마무리 중 남은 것. `BallroomDrapes`: 양 옆벽 1층 창(z 2.5~22.5) 양옆에 크림 캔버스 휘장(`PR_Mid_Canvas` 양면, 폭 0.75·높이 3.95 m, 벽에서 0.12 m)과 창 위 황동 봉·끝 구슬(4.5 m), 35 % 높이 황동 고리. 천은 코드(`SwayingCloth`): 공용 `Configure`에 기본값 0인 `pleat`(세로 주름 깊이, 0.22 m 간격)·`gather`(묶은 허리 당김) 인자를 더함, 다른 방 천은 그대로. 무대 둘레 금 테두리(`Rim W/E/S W/S E/N W/N E`)는 숨기지 않고 무광 황동으로(밝은 금은 게임 신호인 무용수 밑 고리만). 컴파일·Core 162·모의 세션 55 통과. 사용자 확인(11-08 "확인").

## E · 왕궁 서고 (R507, `Mid/e_library_v1.py`)

컨셉 시트 3장은 `Docs/PawnRush/ConceptArt/R507_RoyalLibrary/`, 에셋 시트 2장(흰 배경, Tripo 권장 면 수 포함)은 그 아래 `AssetRefs/`에 있다. 사용자가 만든 Tripo FBX 2개는 `Mid/Source/R507_StackedWoodenBookcases.fbx`(탑 3종)와 `R507_AntiquePropCollection.fbx`(문틀·회전 책장 문·손종·램프·꼭대기 발판)이다. 둘 다 10만 삼각형에 4096 텍스처다. **열람등은 쓰지 않는다**(사용자 11-07: "책장 위에 램프는 두지 않을거야"). 꼭대기 표시는 게임 규칙 그대로 손종이다.

```
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python e_library_v1.py -- --render --export
"C:/Program Files/Blender Foundation/Blender 5.2/5.2/python/bin/python.exe" -I e_install_unity.py
```

- **탑은 통째로 쓰지 않는다.** Tripo 탑은 아래층이 높고 위층이 낮으며, 위로 갈수록 폭도 좁아진다. 게임 탑은 층마다 2.4 m 폭에 3 m 높이(7.5 m 탑의 마지막 층만 1.5 m)이고, 뒷면은 곧게 서며 아래층일수록 남쪽으로 0.8 m 깊다. 그래서 세 탑의 아래층과 중간 탑의 낮은 꼭대기를 **책장 칸 모듈**로 잘라 게임의 층 상자에 하나씩 맞춘다. 2.4 m보다 깊은 층(3.2·4 m)은 같은 칸 두 개를 앞뒤로 붙인다. 앞 칸은 1 cm 넓혀 옆면이 같은 평면에 놓이지 않게 하고, 깊이로 늘리지 않으므로 책이 찌그러지지 않는다.
- Tripo는 아래층 윗면을 위층이 얹히던 구겨진 면과 찢어진 테두리 조각으로 만들었고, 책장 칸은 속이 비어 들여다보인다(사용자 11-07 "깨진 부분 고쳐"). 그래서 층마다 윗면 전체에 B의 호두나무 마루 재질(`PR_Mid_WalnutFloor`)로 두께 12 cm 뚜껑을 덮었다. 뚜껑은 게임 꼭대기보다 2 cm 높고 상자보다 1 cm 안쪽이며, 폰이 밟는 턱도 이 뚜껑이다. 책 뒤로는 면마다 0.45 m 안쪽에 호두나무 속채움을 넣었다.
- 꼭대기 난간은 **Blender에서 새로 만든다**(`rail()`). Tripo 발판은 난간 막대가 찢어지고 기둥과 이어지지 않아(사용자 11-07 "난간도 이어지지 않아") 쓰지 않는다. 무광 황동(B의 `PR_Mid_MatteBrass` 견본) 기둥 9개(모서리, 변 가운데, 입구 양옆), 손잡이 막대와 발 막대, 약 0.26 m 간격 난간동자다. 네 면을 기둥끼리 잇고, 오르는 남쪽 가운데 1.2 m만 입구로 연다. 높이는 0.72 m이고, 꼭대기 층 뚜껑 위에 선다. 난간은 그림이라 충돌이 없다. **그 뒤 사용자가 지우기로 했다**(11-07 "제일 위에 난간 그냥 지우자"). 꼭대기는 뚜껑과 손종만 남고, `rail()`은 참고용으로 스크립트에 남겼다.
- 회전 책장 문은 경첩 기둥을 x = 0에 두고 문짝을 +x로 3.8 × 4.6 × 0.45 m로 맞췄다. 책 면이 남쪽이다. 문틀은 구멍 4.5 m, 아치 꼭대기까지 5.8 m, 기둥 0.9 m, 깊이 1 m다(모델은 위로 7.2 m).

| 부품 | Tripo 원본 | 게임에서 | 손질 뒤 | 판정 |
|---|---|---|---|---|
| 책장 칸 모듈 4종(낮은 탑·중간 탑·높은 탑 아래층, 1.5 m 꼭대기) | 1.6만 · 1.9만 · 2.0만 · 4천 | 한 팀 26칸(탑 6개), 가까이서 오름 | 각 6,000(3,000·4,000은 책과 얇은 테두리가 찢어짐, 사용자 11-07) | 줄이기 |
| 꼭대기 발판 | 1.7만 | 쓰지 않음(난간을 새로 만듦) | - | - |
| 손종 | 6,800 | 한 팀 6개, 울리면 흔들림 | 3,000 | 줄이기 |
| 회전 책장 문 | 5.0만 | 팀마다 1개, 열릴 때 가까이 봄 | 7,000 | 줄이기 |
| 문틀 | 1.6만 | 팀마다 1개 | 2,500 | 줄이기 |
| 열람등 | 1.7만 | 쓰지 않음 | - | - |

늘려야 할 부품은 없다. `E_StationEnv`(탑 6개 + 뚜껑·속채움)는 15.2만 삼각형이고, 두 팀이 같이 쓴다.

**Unity 연결**(`Assets/Scripts/PawnRush/MiniGameModels.Library.cs`, `MiniGameModels`를 partial로 나눔):

- `E_StationEnv`는 각 팀 게임 프레임(x = v, z = u)에 건다. 게임의 탑 층 상자, 그 위 코드 책장 면, 종 기둥·들보는 그리기만 끈다. 충돌은 그대로다.
- `E_Bell`은 꼭대기마다 북쪽 0.55 m, 뚜껑 위 0.5 cm에 세운다. 게임의 종 원통(과 R312 열람등 장식)은 그리기만 끈다. 게임은 종을 울리면 이 원통의 재질을 금으로 바꾸는데, `MiniGameBellLook`이 그 재질을 지켜보다가 손종을 금빛(`PR_Mid_BellRung`, 기능 금·발광 0.6)으로 바꾸고 1.4초 흔든다. 초기화하면 원래대로 돌아온다. 네트워크 값은 새로 만들지 않았다.
- `E_ShelfDoor`는 `DEC_ShelfDoorPivot`에 건다. 팀 문이 열리면 `PalaceGateLeaf`가 그 축을 돌리므로 모델도 함께 돈다. 코드 책장 문짝은 그리기만 끈다.
- `E_DoorArch`는 구간 (±12, 0, 23.8)에 세운다. 셸의 "Team portal front" 아치는 그리기만 끈다.
- **바닥**(`LibraryFloors`, 컨셉 `E_01_overview.png`대로): B가 구운 2단계 바닥(2 m 타일)을 게임의 바닥 상자에 입힌다. 팀 구역·팀 문 통로는 짙은 슬레이트(`PR_Mid_SlateFloor`), 입구 띠·가운데 통로는 크림·회색 체크 대리석(`PR_Mid_CheckerMarble`), 뒤 복도는 크림 대리석(`PR_Mid_CreamFloor`)이다. 셸이 입구 띠와 뒤 복도에 깔던 코드 바닥 판(`ENV_EntryAndRearPaving`)은 그리기만 끈다. 충돌과 금 테두리는 그대로다. Unity 화면 확인 전.
- 확인: `Tools/Test-NetworkCompile.ps1` 전체 어셈블리 컴파일 통과, `Test-NetworkCore.ps1` Core 162·모의 세션 55 통과. 렌더는 `Mid/render_e_overview/entry/side/top/door/ledge/rail/underside.png`. **사용자가 Unity에서 확인했다(2026-11-07, 뚜껑·속채움·황동 난간·6,000면 칸까지).**

### E 방 벽·대문 (R507, `Mid/e_hall_v1.py` → `Export/E_HallEnv.fbx`)

GPT 에셋 시트 `E_assets_03_walls.png`·`E_assets_04_gate.png`로 사용자가 만든 Tripo 세트 2개를 썼다. 원본은 `Mid/Source/R507_ModularArchitecturalElements.fbx`(3만 삼각형: 1층 창 벽, 회색 책 벽 책장, 2층 창 벽, 발코니, 창 없는 벽)와 `R507_ModularFantasyKit.fbx`(5만 삼각형: 가운데 대문, 아치, 천장 들보, 처마)이다. 대문 세트는 아치와 들보가 한 덩어리로 붙어 있어 x −0.028에서 면 단위로 잘랐다.

B처럼 E의 R312 셸을 모두 치우고(사용자 11-07: 기존 구조물은 치워도 되고 벽은 빈틈없이), 모델과 채움 벽으로 방 전체를 다시 세웠다. 구간 좌표(x, 구간 z, 높이)를 쓴다.

- **옆벽**(|x| 22.0 앞면, 본실 z 0~25): 5 m 모듈 5칸, 3 %씩 겹친다.
  - 1층 창 벽: 0~5 m
  - 그 앞 회색 책 벽 책장: 3.6 × 0.5 × 1.7 m
  - 발코니: 바닥 5.3 m, 1.3 m 내민다
  - 2층 창 벽: 5.3~11.7 m
  - 처마: 11.7~12.6 m
  - 뒤 복도(z 30~36)에는 창 없는 벽 1칸
- **북쪽 벽**(z 25.0):
  - 가운데 대문: 16 × 11.8 m, 앞면 z 23.69. 팀 문틀 기둥(|x| 8.85~)을 피한다.
  - 바깥쪽(|x| 14.22~22.6): 창 없는 벽 2칸
- **입구 쪽 벽**(z −0.05): 창 없는 벽 3칸씩
- **입구·출구 아치:** 안쪽 폭 12.4 m(게임 통로 12 m), 기둥 1.2 m, 안쪽 꼭대기 8.5 m. 처음엔 대문 세트의 아치(깊이 약 5 cm 부조)를 1.2 m로 늘려 썼는데 기둥 옆면이 번지고 울퉁불퉁해져(사용자 11-07 "여기 깨져 있어") Blender에서 새로 만든 돌 아치로 바꿨다(`build_arch`: 호두나무 받침 1 m, 크림 대리석 기둥·띠, 반원 아치돌 15개와 머릿돌, 크림 대리석 `PR_Mid_CreamFloor`)
- **처마:** 북쪽·입구 쪽 벽 전체에 두른다.
- **천장 들보:** z 5·10·15·20에 방을 가로지른다. 원본은 막대 양 끝 밑에 받침·기둥이 붙은 모양이다. 막대는 받침에서 찢어진 조각이 없는 가운데 60 %만 6토막으로 잇고, 받침 끝은 양쪽 벽에만 둔다.
- **채움 벽:** B의 `PR_Mid_AshlarFill`(크림 돌, 2 m 타일)로 남는 틈을 13 m까지 메운다(B `b_shell_v1.py`와 같은 방식). 창 뒤에는 구멍을 내 밖이 보이게 했다. 북쪽 덩어리의 팀 문 통로(|x| 9.78~14.22, 높이 5.8 m) 벽·천장과 입구·출구 구멍은 열어 둔다.
- **천장:** 13~13.4 m 호두나무 판(`PR_Mid_WalnutFloor`)
- **바닥 틈 메우기**(사용자 11-07 "바닥에 틈 안생기게 해"): 게임 바닥이 새 벽 앞에서 끝나는 곳을 같은 바닥 재질 띠로 채운다. 팀 구역 슬레이트(높이 0.4 m)는 z 24에서 끝나는데 북쪽 벽은 z 25라 1 m(팀 문 통로 x ±10~14와 가운데 대문 아래 빼고), 입구 벽 앞 5 cm(체크), 북쪽 덩어리 뒤와 뒤 복도 사이 5 cm(크림). 위면은 게임 바닥보다 2~5 mm 낮다(겹친 면 깜빡임 방지).
- **면 수:** 전체 23.8만 삼각형(채움 벽·천장 포함). 원본 두 세트는 부품마다 넉넉해 줄이기만 했다(벽 모듈 2,500~3,000, 대문 20,000, 아치·들보 3,000, 처마 1,500).
- **연결**(`MiniGameModels.LibraryHall`):
  - `MODEL_Hall`을 구간에 걸고, 셸은 모두 그리기만 끈다. 충돌은 구간 빌더 것 그대로다.
  - 셸의 팀 깃발이 함께 사라지므로, B처럼 코드 천(`MiniGameFixtures.Pennant`)으로 팀색 깃발을 팀 문틀 앞(높이 7.0 m, z 23.25)에 건다.
- **종 울림 효과·소리**(사용자 11-07 "종 울릴 때 vfx랑 sfx 적용 해", "일레븐 랩스에서 하면 돼"): `MiniGameBellLook`이 게임 종이 울린 순간에만 낸다(설치 때 이미 울린 종은 조용히 금빛만).
  - 효과(`MiniGameBellRingFx`, 사용자 11-07 "이런 vfx 말고" → "음파 + 음표"): 음파 고리 3겹이 0.16초 간격으로 종에서 4.2 m까지 퍼짐(늘 카메라를 향해 동그랗게), 금빛 8분음표(♪·♫) 5개가 흔들리며 1.7 m 떠올라 사라짐, 종 둘레 발광 번쩍임(0.7초)과 따뜻한 점광원. 텍스처는 `Mid/e_bell_fx_textures.py`가 그린 검은 바탕의 흰 모양(색과 알파 모두 모양: 가산 재질은 색을 그대로 더해서, 색이 전부 흰색이면 흰 사각형이 보였다, 사용자 11-07 "이렇게 나오는데")(`Assets/Resources/PawnRushMiniGames/Fx/T_Bell_Note1/Note2/Wave.png`), 재질은 프로젝트의 `Resources/URPVariants/Particles Additive`에 HDR 금빛을 입혀 블룸이 걸린다. 처음의 `ArenaFx` 고리·반짝이·빛기둥은 뺐다.
  - 소리: ElevenLabs 효과음(`eleven_text_to_sound_v2`, 요청문 "Single strike of a brass hand bell, clear bright metallic ring with a long shimmering decay, in a large wooden library hall") 4개 중 사용자가 1번 선택 → `Assets/Resources/PawnRushMiniGameSfx/sfx_bell_ring.mp3`(약 1초, 3D라 모노로 가져옴)
  - 고친 것(11-07 "똑같아 소리도 안나와"): `MaterialPropertyBlock`을 MonoBehaviour 필드에서 만들면 Unity가 비워 두어, 첫 사각형만 흰색으로 남고 예외 때문에 종소리까지 건너뛰었다. 이제 `Build`에서 만들고, 소리를 효과보다 먼저 낸다.
  - 탑마다 음: 도·레·미·솔·라·높은 도(`MgBells.Towers` 순서, 입구 쪽 낮은 탑부터). `ArenaFx.Sfx`는 음을 ±5 % 흔들어 음계가 틀어지므로 종 자기 AudioSource로 정확한 음을 낸다.
  - **사용자가 Unity에서 확인했다(2026-11-07, 음파·음표·종소리).**
- **책장 문 열림 효과·소리**(사용자 11-07 "문이 열리는 효과도 교체 해"): `MiniGameShelfDoorFx`(스테이션에 붙음)가 공용 완료 효과(팀색 빛기둥·조각·고리, `ArenaDressing`)를 대신한다. 공용 코드에는 `IGateFx` 갈림길만 더했고 다른 미니게임은 그대로다. 4분의 1 진행 효과는 E에서 끈다(종 효과와 겹침).
  - 바닥과 문짝 위에서 먼지(`Lit Transparent`), 통로에서 새어 나오는 금빛과 점광원, 방 쪽으로 흘러나오는 금빛 티끌, 책장 위에서 팔랑이며 떨어지는 종이 7장. 텍스처 `T_Door_Dust/Mote/Glow`(`e_bell_fx_textures.py`).
  - 소리: ElevenLabs "Heavy wooden bookcase secret door swinging open on a brass hinge, slow creak and low rumble, books rattling, ending with a soft thud, in a large library hall" 4개 중 사용자가 4번 선택 → `Resources/PawnRushMiniGameSfx/sfx_shelf_door_open.mp3`.
  - Unity 화면·소리 확인 전.
- **조명**(`Mid/e_lights_v1.py` → `E_LightsEnv`): 셸과 함께 코드 샹들리에도 꺼져서, B의 샹들리에(빈 초받침 8개)·벽등(빈 랜턴 틀)을 같은 원본(`R503_FantasyArchitectural`)과 `b_hall_v1.py`의 함수로 다시 잘라 E 방에 걸었다. 새 Tripo 작업은 필요 없다.
  - 샹들리에 7개: 가운데 통로 위 z 7.5·12.5·17.5, 팀 구역 탑 줄 사이(x ±12) 위 z 7.5·17.5. 사슬 꼭대기 12.9 m, 아래 끝 10.3 m로 탑(손종까지 10.3 m)·들보와 겹치지 않는다.
  - 벽등 10개: 옆벽 1층 창 사이 기둥(z 5·10·15·20, 높이 3.4 m), 북쪽 벽 x ±18.4.
  - 불꽃·불빛은 모델에 없고, `SOCKET_` 자리에 `MiniGameFixtures.LightSockets`가 코드로 붙인다(B와 같다). 재질 `PR_Mid_HallKit`·`PR_Mid_Flame`은 B의 것이다. 7.4만 삼각형. **사용자가 Unity에서 확인했다(2026-11-07, 소품과 함께).**
- **소품**(`Mid/e_props_v1.py` → `E_PropsEnv`, 각 소품은 `E_Prop_<이름>`으로 미리보기): 사용자 지시로 Tripo 없이 Blender에서 직접 만들었다(11-07 "이번 건 너가 블렌더에서 디테일하게 만들어"). 기준은 GPT 시트 `AssetRefs/E_assets_05_props.png`다.
  - 책 수레: 아치 손잡이, 2단, 황동 난간·모서리·바퀴, 앞뒤로 꽂힌 책
  - 굴림 사다리: 둥근 가로대, 황동 걸쇠와 바퀴, 14° 기울어짐
  - 쌓인 책 6권
  - 지구본: 깎은 호두나무 다리 3개, 황동 자오환, 23° 기울어짐
  - 열람 책상: 18° 경사판, 책 받침, 서랍 2개와 황동 손잡이, 가운데 골이 있는 펼친 책
  - 책상과 의자 2개: 깎은 다리, 가죽 방석, 책
  - 책: 표지 2장, 책등, 안쪽으로 들어간 종이 묶음, 황동 띠로 만든다.
- **소품 재질:**
  - 나무: 이음 없는 호두나무 결 `PR_Mid_PropWalnut`(스크립트가 굽는다, 1 m × 0.5 m). B 마루 텍스처의 판자 이음이 다리에 줄로 보여서 따로 만들었다.
  - 지구본: `PR_Mid_PropGlobe`(노이즈 대륙, 해안선, 30° 위경선, 글자 없음)
  - 황동: B의 `PR_Mid_MatteBrass`
  - 가죽·천·종이·쇠·방석: 단색 `PR_Mid_Prop*`
- **소품 배치:** 소품에는 충돌이 없어 폰이 다니는 길을 피했다(입구 → 팀 경사로 x ±9.8~14.2, 가운데 통로, 탑 사이, 뒤 복도 팀 통로 → 출구).
  - 입구 띠 바깥 구석(|x| > 15): 책상과 의자, 책 수레, 지구본, 쌓인 책
  - 옆벽 책장: 사다리(z 7.5)
  - 뒤 복도 양 끝: 열람 책상, 쌓인 책, 책 수레
  - 전체 12.7만 삼각형. 연결은 `MiniGameModels.LibraryHall`(`MODEL_Props`). **사용자가 Unity에서 확인했다(2026-11-07).**
- 렌더 `Mid/render_e_hall_entry/side/back/corner/rear.png`. 컴파일·Core 162·모의 세션 55 통과. **사용자가 Unity에서 확인했다(2026-11-07).**

## H 국왕 체스 홀 (R510)

G와 같은 3단계라 홀은 G 것을 그대로 쓴다(`Docs/PawnRush/ConceptArt/R510_KingsChessHall/REUSE.md`). H만의 것:

1. `Late/h_props_v1.py` — 사용자 Tripo `Late/Source/R510_H_A1_Props.fbx`를 체스 시계·예비 기물 상자·기보 석판·홀 종으로 잘라 손질(`g_props_v1.py` 함수 재사용) → `Late/Export/H`, 렌더 `Late/render_h_props.png`.
2. `Late/h_board_kit_v1.py` — 판 기둥·울타리 기둥·킹 쟁반·천창 우물(`H_SkylightWell`, H는 G 볼트 대신 이 천장)을, `Late/h_crest_v1.py` — 팀 문 위 킹 문장(`H_TeamGateCrest`, G 룩 문장 자리)을, `Late/h_decals_v1.py` — 석판 기보·시계 숫자 글자판(`H_NotationText`·`H_ClockDials`, 글꼴로 렌더한 텍스처)을 Blender로 직접(3단계 재질 이름 슬롯) → `Late/Export/H`, 렌더 `Late/render_h_board_kit.png`.
3. `Late/h_install_unity.py` — `Assets/Resources/PawnRushMiniGames/Late/H`, 재질 `PR_Late_H_Props`·`Ebony`·`Bronze`·`FrozenStone`(발광 맵).
4. `Assets/Scripts/PawnRush/MiniGameModels.ChessHall.cs` — Play 때 배치. 킹 모델은 게임 킹의 `Look`에 매달려 함께 움직이고, `MiniGameKingLook`이 굳은 상태를 따라 재질을 바꾼다. 홀 종은 받침쇠(`H_HallBellBracket`)와 종(`H_HallBellBody`, 원점 = 경첩)으로 나뉘어 `MiniGameHallBell`이 체크(`MgCheckmate.KingChecked`) 때 흔든다. 충돌·규칙은 게임 그대로.

다시 손질하면 1·2 → 3 순서. G 함수(`RoyalProcessionHall` 등)를 바꾸면 H에도 그대로 적용된다.

