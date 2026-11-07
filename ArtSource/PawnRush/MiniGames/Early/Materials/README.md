# 1단계 재질 견본 (R502~)

R501 도개교 모델은 재질마다 색 그림 한 장뿐이었다. 사용자는 [R502 참고 사진 3장](../../../../../Docs/PawnRush/ConceptArt/R502_DrawbridgeSheets/)과 같은 분위기·재질을 원한다. 그래서 먼저 재질 4종을 **실제 입체 견본**으로 만들고, 위에서 똑바로 내려다보며 이어 붙는(타일링) PBR 텍스처로 굽는다.

```
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python r502_material_swatches.py -- --bake --preview
```

- `--bake`: `Textures/`에 맵을 굽는다. `--res N`은 2 m 타일 한 장의 픽셀 수(기본 2048, 금속판 1 m 타일은 그 절반).
- `--preview`: `Textures/`로 견본 판을 렌더한다. `swatch_render.png`(우리 견본), `swatch_compare.png`(위: 참고 사진 3의 재질 견본 줄, 아래: 우리 견본 · 1줄 사진 크기 · 2줄 2×2 반복으로 이음새 확인).
- 중간 EXR은 임시 폴더(`%TEMP%/r502_swatch_work`)에 둔다.

| 재질 | 타일 | 맵 | 입체 견본 |
|---|---|---|---|
| `PR_Early_StoneBrick` 크림 돌 벽돌 | 2 m | BaseColor · Normal · Roughness · AO · Height | 0.4 m 줄 5개, 0.38~0.78 m 블록. 둥글게 부푼 윗면, 큰 베벨, 닳은 모서리, 블록마다 색 차이, 줄눈 |
| `PR_Early_WoodPlank` 나무 판자 | 2 m | BaseColor · Normal · Roughness · AO · Height | 0.25 m 판자 8줄, 홈 1.4 cm, 판자마다 휨·색 차이, 나뭇결, 끝 닳음 |
| `PR_Early_MetalPlate` 리벳 어두운 금속판 | 1 m | BaseColor · Normal · Roughness · AO · Height · Metallic | 판 한 장 + 모서리 리벳 4개, 두드린 면, 얼룩·녹 점 |
| `PR_Early_Water` 물 | 2 m | Normal · Caustic | numpy로 만든 주기 함수(물결 노멀, 빛 무늬). 색은 Unity 물 셰이더가 정한다 |

지키는 것:
- 견본 물체를 한 주기씩 옆 8칸에 같이 놓고 구우므로 모양·AO·필터가 타일 가장자리에서 이어진다.
- 노멀 맵은 OpenGL 방식(G = 위), Unity 기본과 같다. 베이스 컬러에는 빛·그림자를 넣지 않는다([3단계 아트 기준 §4](../../../../../Docs/PawnRush/PAWN_RUSH_ART_PHASES.md)).
- 도개교에는 **장식 금을 쓰지 않는다**(R502 사용자 지시). 견본에도 금은 없다. 게임이 쓰는 금 손잡이·눈금은 시스템 표시라 그대로다.
- 아직 Unity `Assets/`로 옮기지 않았다. 사용자가 견본을 확인한 뒤 2단계(Unity 조명·후처리·물 셰이더)에서 옮긴다.

## 벽·바닥 모듈: 돌 블록 모양 + 성탑 재질 (`r502_tower_block.py`)

사용자가 Tripo GLB 2개를 주었다(원본은 `../Source/`). 결정: **모양은 돌 블록, 재질은 성탑 그대로.**

```
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python r502_tower_block.py -- --extract --block --preview
```

- `--extract`: 성탑 정면 벽을 칠한 색 그대로(정사영) 찍고, 벽돌 안쪽만 골라 이어 붙는 돌 표면 타일(`PR_Early_TowerStone_Surface.png`, 0.5 m)을 만든다. 줄눈색·돌색·거칠기(0.67)는 `PR_Early_TowerStone.json`.
- `--block`: 돌 블록을 1 m 폭 모듈(1.0 × 0.74 × 1.09 m, 정면 y = 0, 바닥 z = 0)로 맞추고 6천 삼각형 게임용으로 줄여, 원본 조각(35만 삼각형)에서 `PR_Early_StoneBlock_BaseColor/Normal/Roughness/AO.png`(2048)를 굽는다. 틈은 성탑 줄눈색, 모서리는 살짝 밝게. `StoneBlock_Module.blend`에 게임용 모듈이 있다.
- `--preview`: `render_tower_block.png`(성탑 옆 모듈), `render_wall_floor.png`(벽 7×2, 바닥 7×4는 블록 정면을 위로, 1 m 폰).
- 벽은 모듈 절반을 뒤로 돌려 쌓아 무늬가 반복돼 보이지 않게 한다. 아직 FBX·Unity로 옮기지 않았다.
- 줄눈: 사용자 요청("조금만 메워줘")으로 블록 안에 줄눈 심 상자를 넣었다. 앞면에서 잰 줄눈 깊이 4.8 cm 중 40%(`MORTAR_FILL`)만 메워 모든 면 3.3 cm 안쪽에 있다. 게임용 모듈에도 합쳐져 있다.

## Unity로 (2단계, `r502_export_unity.py`)

```
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b StoneBlock_Module.blend --python r502_export_unity.py
```

- `Assets/Resources/PawnRushMiniGames/Early/R502/`에 `PR_Early_StoneBlock.fbx`와 `Textures/`(돌 블록 색·노멀·AO·금속도/매끈함, 물 노멀·빛 무늬)를 .meta와 함께 쓴다.
- Unity 메뉴 **ChessFight → Pawn Rush → Development → R502 Early Look Test**(`Assets/Scripts/PawnRushEditor/EarlyLookTestMenu.cs`)는 다음을 만든다.
  - 재질 `PR_Early_StoneBlock.mat`(URP Lit), `PR_Early_MoatWater.mat`
  - 1단계 볼륨 `PR_Early_Look.asset`: 따뜻한 화이트 밸런스, 노출 +0.15, 대비 8, 채도 12, 약한 Bloom·비네트
  - 위 셋은 처음 한 번만 만들고, Unity에서 고친 값은 유지한다.
  - 시험 장면 `Assets/Scenes/SeoungTest/R502_EarlyLookTest.unity`는 매번 새로 만든다.
- 물 셰이더는 `Assets/Art/PawnRushMap/Shaders/PR_MoatWater.shader`이며, 1단계 노천 물 전용이다.
  - 물 두께에 따라 청록색이 진해지고, 굴절이 있다.
  - 물속 바닥에 빛 무늬가 맺히고, 물이 얕은 곳에 거품이 생긴다.
  - URP 깊이·불투명 텍스처가 필요하다(PC_RPAsset에서 켜져 있음).
- 사용자가 준 권양기 `../Source/R502_WoodenGearWinch.fbx`는 8만 삼각형 한 덩어리이고, 축 끝 마개가 금색이다. 4단계 장치에서 드럼(게임이 돌리는 부분)과 틀을 나눠 쓴다.

## C 도개교 에셋 면 수 예산 (R502, 사용자 11-07: "폴리곤 수 높여야 할 거 낮춰야 할 거 구분해서")

모두 Tripo에서 따로따로 넉넉한 면 수로 받아서, **늘릴 부품은 없고 전부 줄인다.** 줄이기는 Blender에서 부품별 목표로 decimate하고, 세부는 노멀 맵으로 굽는다(돌 블록 모듈과 같은 방식). 목표 기준은 D 세션이 정리한 표(반복 타일 300~1,500, 벽·모듈 1,000~1,500, 둥근 소품 3,000~6,000, 가까이 보는 장치 3,000~15,000, 큰 건물 10,000~30,000)를 따른다.

| 부품 (원본) | 지금 삼각형 | 게임에서 (크기 · 개수 · 움직임) | 목표 | 판단 |
|---|---|---|---|---|
| 성탑 `R502_CastleTower_Tripo.glb` | 906,140 | 높이 약 18.5 m · 뒤쪽 2개 · 고정 | 50,000 | 줄이기 |
| 성문(양 통로 뚫음) `R502_RoyalCastleGate_Open.blend` | 458,260 | 폭 35 m · 정면 1개 + 위층 회랑 1개 · 고정 | 80,000 | 줄이기 |
| 돌 블록 모듈 `R502_StoneBlock_Tripo.glb` | 원본 356,543 / 지금 게임용 6,000 | 1 m 모듈 · 바닥·벽·해자에 수백 개 반복 | 3,000 (바닥은 평면 + 구운 텍스처로 바꾸는 것도 검토) | 더 줄이기 |
| 권양기 `R502_WoodenGearWinch.fbx` | 80,000 | 폭 약 3 m · 2개 · 드럼이 돎, 가까이 봄 | 25,000 (드럼 따로) | 줄이기 |
| 정원 돌담 난간 `R502_GardenStoneWall.fbx` | 500,000 | 3.4 m 한 칸 · 양옆 약 14칸 · 고정, 사슬·꽃 포함 | 15,000 | 줄이기 |
| 도개교 키트 `R502_DrawbridgeWalls.fbx` → `R502_DrawbridgeKit_Fixed.blend` | 500,000 (부품 5개 합) | | | |
| ├ 내려간 다리(기둥·사슬 포함) | 약 150,000 | 4 × 4.1 m · 팀마다 1개 · 힌지로 돎, 가까이 봄 | 30,000 | 줄이기 |
| ├ 세운 다리(힌지 드럼) | 약 130,000 | 상판은 위 다리와 같은 것을 씀 · 힌지 드럼만 참고 | 15,000 | 줄이기 |
| ├ 레일 바닥 | 약 100,000 | 밧줄 길 · 2개 · 고정 | 8,000 | 줄이기 |
| ├ 가운데 난간(금 문장) | 약 73,000 | 폭 약 4 m · 1개 · 고정 | 12,000 | 줄이기 |
| └ 망루 기둥(사슬 고리) | 약 36,000 | 높이 3.3 m · 4개 · 고정 | 12,000 | 줄이기 |

사용자 요청(11-07 "목표보다 좀 더 높여")으로 목표를 처음 안의 2~3배로 올렸다. 줄이는 순서: 평평한 면만 먼저 합치고(평면 decimate), 그다음 목표까지 줄이며, 부품마다 원본과 나란히 렌더해 톱니·사슬 고리·꽃잎·흉벽 모서리가 뭉개졌으면 그 부품만 더 올린다. 키트 부품의 삼각형 수는 꼭짓점 수에서 어림한 값이다. 실제 줄이기는 5단계(Unity로 옮길 때)에서 하고, 줄인 뒤 렌더로 모양이 뭉개지지 않았는지 확인한다.

## A 의장 광장 견본 (R506, `r506_material_swatches.py`)

[R506 컨셉 시트](../../../../../Docs/PawnRush/ConceptArt/R506_CeremonialCourt/)의 재질 견본 줄 9칸에 맞춘다. 위 R502 견본과 같은 방식(입체 견본을 위에서 구워 이어 붙는 PBR 맵)이고, 파일 이름이 겹치지 않게 A 전용은 `PR_A_`, 1단계 공용은 `PR_Early_`로 둔다.

```
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python r506_material_swatches.py -- --bake --preview
```

- `--only 이름`으로 하나만 굽는다. `--res N`은 2 m 타일 한 장의 픽셀 수(기본 2048, 지붕 1 m 타일은 그 절반).
- `--preview`: `r506_swatch_render.png`(우리 견본), `r506_swatch_compare.png`(위: 컨셉 견본 줄, 아래: 1줄 1 m 크기 · 2줄 2×2 반복). R502의 `swatch_*.png`는 건드리지 않는다.
- 중간 EXR은 `%TEMP%/r506_swatch_work`.

| 재질 | 타일 | 쓰는 곳 | 만든 법 |
|---|---|---|---|
| `PR_A_Slate` 슬레이트 | 2 m | 칠하기 전 칸(게임 `Arena.Slate` #4A5060) | 이어진 평평한 판(밟는 면이라 출렁임은 2 mm 이하). 결 노이즈를 7단 계단으로 끊은 층 무늬(주로 색), 살짝 도드라진 밝은 박리 조각, 잔 알갱이 |
| `PR_A_IvoryLimestone` 상아 석회암 | 2 m | 흰 팀이 칠한 칸(게임 #F3EADB) | 이어진 판. 옅은 구름 무늬, 굵고 가는 잎맥 두 겹, 작은 구멍 |
| `PR_A_Basalt` 검은 현무암 | 2 m | 흑 팀이 칠한 칸(게임 #231E1B) | 이어진 판. 밝은 광물 알갱이, 기공(움푹한 점), 반쯤 갈린 면 |
| `PR_A_CourtPaving` 광장 크림 포장 | 2 m | 체스판 둘레·광장 바닥 | 0.40~0.60 m 줄 4개에 0.5~1.0 m 판석, 줄눈 1.2 cm, 판석마다 색 차이·살짝 볼록·닳은 모서리 |
| `PR_Early_RoofSlate` 회색 지붕 슬레이트 | 1 m | 1단계 지붕 전부(A 정문·탑, D 누각도 쓸 수 있음) | 0.25 m 줄 4개, 폭 0.18~0.28 m 기와가 아래로 겹침, 아래 끝 깨짐 |

다시 쓰는 것(여기서 굽지 않음): 크림 돌 벽돌 `PR_Early_StoneBrick`·참나무 `PR_Early_WoodPlank`(R502), 무광 황동 `PR_Late_Brass`(R504, `../../Late/Materials/Textures`). 금 줄눈은 게임의 기능 금(#E2B23C, 1단계 자체발광 0.4) 그대로라 텍스처가 없다.

- 칸 판·칠하는 판·줄눈은 게임 코드 `PaintBoard`가 그린다. 판정·크기는 그대로 두고 재질만 바꾼다(아직 Unity에 옮기지 않음).
- 세 칸 재질은 한 칸(2.15 m 판)에 2 m 타일이 거의 한 장 들어간다. 칸마다 UV를 조금씩 밀면 이웃 칸이 같은 무늬로 보이지 않는다.

## C 도개교 v2 → Unity (R502, "유니티적용")

```
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python ../c_drawbridge_v2_export.py
"C:/Program Files/Blender Foundation/Blender 5.2/5.2/python/bin/python.exe" -I ../c_install.py
```

- `c_drawbridge_v2_export.py`: `c_drawbridge_v2.build()` 배치를 위 면 수 목표로 줄이고, Unity가 코드로 만드는 것(물·깃발·빛나는 유리·밧줄·매듭)을 뺀다. 등불 자리에는 `SOCKET_Lantern`만 남긴다. `Export/C_Drawbridge_EnvV2.fbx`(구간 기준, 물체 480개·삼각형 약 214만)와 `C_Drawbridge_DeckV2.fbx`(힌지 기준)를 쓴다.
- `c_install.py`: FBX는 `Assets/Resources/PawnRushMiniGames/Early/C/`, 텍스처는 `Assets/Art/PawnRushMiniGames/Early/Textures/`, 재질은 `Assets/Resources/PawnRushMiniGames/Materials/PR_Early_C_*.mat`(+ `PR_Early_StoneBlock`)에 둔다. D의 `PR_Early_DarkIron`은 함께 쓴다.
- Unity 연결: `Assets/Scripts/PawnRush/MiniGameModels.Drawbridge.cs`. 모델을 붙이고, 옛 그림(셸·바닥·해자 벽·망루·권양기 틀·드럼·밧줄 길)은 그리기만 끈다. 해자 물 한 장, 등불 불빛, 흔들리는 깃발은 코드로 만든다.
- 게임 코드: `MgDrawbridge`(궁전 구간)는 밧줄 길을 x ±11에, 권양기를 입구 쪽 u 1.4(밧줄 길 바깥 3.1 m)에 둔다. 판정·매듭 이동·통신은 그대로다.
