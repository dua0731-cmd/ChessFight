# 마차뜰 재질 · 텍스처 계획 (R686, seoungtest, 2026-11-17)

승규 "재질 텍스쳐 컨셉 아트대로 재질 그래픽 극대화로 나오게 구워", "작업 속도를 위해 … 2개 세션을 추가한다", "코덱스에게 모든 재질 텍스쳐를 받아 내".
세 세션이 이 파일 하나를 따른다. **사용자 Unity 확인 전.**

| 세션 | 맡은 것 |
|---|---|
| rcy-textures (이 파일을 씀) | 재질 라이브러리(Codex albedo → 타일 · 노멀 · 부드러움), 공용 스크립트(`rcy_kit.py` UV · 칸 상수, `export_rcy.py`, `rcy_textures.py`, `textures/rcy_unity_materials.py`), Unity 연결(`RoyalCarriageYard.Dressing.cs`, `YardKit.cs` 재질), 견본 = 입구(RcyEntrance), Blender MCP |
| rcy-tex-canal-sorting | RcyCanal · RcyGiantWaterwheel · RcySorting · 기계 15개 |
| rcy-tex-movers-castle | 마차 · 바퀴 · 고리 · 문짝, RcyHorse, 승강장 · 결승 마당 · 구간 바닥, 배경, 성 3개 |

공용 파일(`rcy_kit.py`, `export_rcy.py`, `rcy_textures.py`, `textures/*`, `Dressing.cs`)은 rcy-textures만 고친다. 다른 세션은 SendMessage로 요청한다.
각 세션은 자기 `*_model.py`의 **재질 칸 이름만** 고치고 FBX를 다시 낸다(`qa/rebuild_models.py -- <키>`).

## 1. 방식 (한 줄 요약)

**재질 칸마다 이어지는 타일 1세트(라이브러리) + 모델은 부품마다 미터 단위 UV.** 구역마다 따로 굽는 고유 텍스처나 아틀라스는 쓰지 않는다.
- 이유: 구역 하나가 40 ~ 50 m(분류장 바닥만 2.7만 m²)라 고유 굽기로는 텍셀 밀도가 안 나온다. 블록 · 판석 · 판자 · 기와는 이미 모양(베벨)으로 있어서 텍스처는 "돌 한 덩이 · 판자 한 장의 표면"만 그리면 된다.
- 그리기 횟수: FBX가 이미 재질 칸별로 따로 그려지므로 칸마다 재질을 달아도 늘지 않는다(SRP Batcher). 재질 수는 마차뜰 전체에서 라이브러리 개수(아래 표, 24개 이하) + 성 키트 5개.

## 2. UV 규칙 (`rcy_kit.surface_uvs`)

- `rcy_kit.join()`의 끝에서 자동으로 건다(`RCY_UVS = True`). join을 거치는 모델은 아무것도 안 해도 된다. join을 안 거치는 메쉬(말 등)는 bmesh에 `surface_uvs(bm)`를 직접 부른다(월드 좌표에서, 내보낼 좌표 그대로).
- 이어진 조각(돌 하나, 판석 하나, 판자 하나, 기와 줄 하나)마다: 가장 넓은 면의 법선 = 축 1, 그에 수직인 가장 넓은 면 = 축 2, 외적 = 축 3. 면마다 가장 마주 보는 축 평면에 투영.
- **U = 조각의 가장 긴 축**(나뭇결이 판자 · 들보 · 기둥을 따라 흐름). 끝면은 남은 두 축 중 긴 쪽이 U.
- **단위 = 미터.** Unity 재질 타일링 = 1 / 타일 m(아래 표). 텍셀 밀도는 큰 면 1024 px / 2 m ≈ 512 px/m(PC).
- 조각마다 위치로 정한 무작위 오프셋(0 ~ 16 m): 블록마다 색이 조금씩 다르고 타일 반복이 줄을 안 맞춘다. 같은 소스를 다시 지으면 같은 UV.
- `split_cells`는 UV를 그대로 옮긴다(칸 경계에서 잘린 긴 부품도 이어짐).
- 성 키트(AI, 자기 UV · 텍스처)는 다시 굽지 않는다. 색이 라이브러리와 크게 어긋나면 재질 `_BaseColor`로만 맞춘다(rcy-tex-movers-castle 판단 → 요청).

## 3. 재질 표 (라이브러리)

색은 컨셉 시트 RCY_08 아래 재질 견본 줄 · 승규 그림 3장에서 뽑음. albedo는 **Codex image_gen**(`textures/codex/b1 ~ b3/`, 프롬프트 `PROMPT.md`)에서 받고, `rcy_textures.py`가 이음매 없게 만들고(Moisan 주기 분해) · 넓은 조명 얼룩을 펴고 · 평균색을 계획 색 쪽으로 반만 당기고 · 그 albedo의 세부에서 높이 → 노멀 · 부드러움을 만든다. Codex 그림이 없는 칸은 절차적 타일(같은 파일 `SPECS`)로 대신한다.

| 라이브러리 이름 | 재질 칸 (`*_model.py`) | 쓰는 곳 | 컨셉 견본 · 모습 | 목표 평균색 | 타일 m | 금속 | 부드러움 | Codex 파일 |
|---|---|---|---|---|---|---|---|---|
| RCY_Stone | `RCY \| stone` (STONE) | 벽 · 갓돌 · 흉벽 · 탑 | 크림 석회암: 따뜻한 크림, 크게 번지는 색, 얕은 구멍 | #DCBC93 | 2 | 0 | .22 | rcy_cream_limestone.png |
| RCY_Flag | `RCY \| flag` (FLAG) | 바닥 판석 · 연석 · 바닥 띠 (`flagstones` · `setts_line` 기본값) | 판석: 벽보다 붉고 닳아 매끈, 가는 금 | #D6B590 | 2.4 | 0 | .24 | rcy_flagstone.png |
| RCY_Shade | `RCY \| shade` (SHADE) | 받침돌 · 주춧돌 · 물 아래 돌 · 바닥 밑판 | 그늘 돌: 따뜻한 중간 회색 | #A1958D | 2 | 0 | .2 | rcy_shade_stone.png |
| RCY_Marble | `RCY \| marble` (MARBLE) | 운하 난간 · 절벽 · 대리석 띠 | 흰 대리석: 사선 회색 결 | #E8E0DB | 3 | 0 | .6 | rcy_white_marble.png |
| RCY_Tile | `RCY \| tile` | 곳간 · 판매대 · 다락 지붕 | 슬레이트 표면(모양은 기와 줄), 파란색 없음 | #766C65 | 1.5 | 0 | .22 | rcy_roof_slate.png |
| RCY_Soil | `RCY \| soil` (SOIL) | 맵 가장자리 흙(있으면) | 다진 흙 | #A58A66 | 2 | 0 | .1 | rcy_soil.png |
| RCY_Oak | `RCY \| oak` (OAK) | 상자 · 통 · 처마판 · 승강대 틀 | 밝은 참나무: 꿀색, 흐르는 결 | #CA9054 | 2 | 0 | .3 | rcy_light_oak.png |
| RCY_Walnut | `RCY \| walnut` (WALNUT) | 기둥 · 들보 · 바닥판 · 문짝 · 마차 틀 | 호두나무: 붉은 갈색 | #7C4424 | 2 | 0 | .32 | rcy_walnut.png |
| RCY_Timber | `RCY \| timber` (TIMBER) | 크레인 목재 묶음 · 장선 · 받침틀(날것의 켠 나무) | 밝은 켠 나무 | #C4A374 | 2 | 0 | .2 | rcy_pale_timber.png |
| RCY_Iron | `RCY \| iron` (IRON) | 띠쇠 · 등불 틀 · 사슬 · 리벳 | 검은 쇠: 망치 자국, 녹 조금 | #554A45 | 1 | .75 | .42 | rcy_dark_iron.png |
| RCY_Brass | `RCY \| brass` (BRASS) | **장식 금속 전부**(띠 · 테두리 · 명판 · 처마 테두리) | 무광 황동 | #9E7135 | 1 | .9 | .45 | rcy_matte_brass.png |
| RCY_Gold | `RCY \| gold` | **왕관 장식만**(+ 모델이 그리는 기능용 금이 있으면) | 금박: 반짝임 줄임 | #BE8F38 | 1 | 1 | .5 | rcy_gold.png |
| RCY_Coach | `RCY \| coach` | 마차 차체 칠 | 병 녹색 옻칠 | #375231 | 1.5 | 0 | .58 | rcy_coach_lacquer.png |
| RCY_Cloth | `RCY \| cloth` | 차양 · 덮개 · 휘장(안 흔들리는 것만) | 모래색 캔버스 | #B68E60 | 1 | 0 | .14 | rcy_sand_canvas.png |
| RCY_Sack | `RCY \| sack` | 자루 | 마대 | #9A784B | .8 | 0 | .12 | rcy_burlap.png |
| RCY_Straw | `RCY \| straw` | 건초 | 짚(가로 줄기) | #C9A352 | 1 | 0 | .16 | rcy_straw.png |
| RCY_Rope | `RCY \| rope` | 밧줄(모델에 있는 것) | 삼 밧줄(사선 가닥) | #9C7B4F | .4 | 0 | .14 | rcy_rope.png |
| RCY_Leather | `RCY \| leather` | 마구 · 끈 · 좌석 | 짙은 안장 가죽 | #59331F | .6 | 0 | .42 | rcy_leather.png |
| RCY_Velvet | `RCY \| velvet` | 마차 안 · 쿠션 | **짙은 병 녹색 벨벳**(빨강 아님) | #24361F | .6 | 0 | .25 | rcy_velvet.png |
| RCY_Coat | `RCY \| coat` | 말 털 | 무채색 짧은 털 → Dressing.cs가 털 색(갈색 · 회색 · 흑)을 곱함 | #BDBDBD | .6 | 0 | .35 | rcy_horse_coat.png |
| RCY_Hair | `RCY \| hair` | 갈기 · 꼬리 | 무채색 긴 털 → 털 색을 곱함 | #9A9A9A | .6 | 0 | .3 | rcy_horse_mane.png |
| RCY_Hoof | `RCY \| hoof` | 발굽 | 짙은 뿔 | #3A302A | .3 | 0 | .5 | rcy_hoof.png |
| RCY_Plume | `RCY \| plume` | 깃털 장식 | 크림 깃털 | #F1E8D6 | .4 | 0 | .2 | rcy_plume.png |
| (없음) | `RCY \| eye` | 말 눈 | 코드 색 그대로(#0E0B0A, 매끈 .85) | — | — | — | — | — |
| (없음) | `RCY \| clay` 등 표에 없는 칸 | — | 클레이로 그려짐 → 표의 칸으로 바꿀 것 | — | — | — | — | — |

**칸 이름 규칙(모델 소스에서 고칠 것):**
- 바닥 판석 = FLAG(rcy_kit `flagstones` 기본값이 이미 FLAG). 벽 = STONE. 받침돌 · 주춧돌 · 물 아래 돌 · 보이지 않는 밑판 = SHADE.
- **장식용 gold → BRASS.** gold는 왕관(마차 지붕 · 차양 위 왕관 같은)에만 둔다. 컨셉 규칙 "밝은 금 = 밟고 · 잡고 · 뛰는 곳, 장식 금속은 어두운 무광 황동". 코드가 그리는 기능용 금(금 테두리 · 손잡이 · 점프 줄)은 모델이 아니므로 그대로.
- 날것의 켠 나무(목재 묶음 · 장선)는 TIMBER, 다듬은 밝은 나무는 OAK, 짙은 나무는 WALNUT.
- 불빛 · 빛나는 유리 · 흔들리는 천은 모델 · 텍스처에 넣지 않는다(Unity 코드).

**다른 미니게임 텍스처 재사용 검토(11-17):** B · E · G · H 방의 `PR_*`(벽돌 · 호두나무 바닥 · 황동 · 슬레이트 · 캔버스 등)는 대부분 바닥용이라 블록 · 판자 줄눈이 그려져 있어 모양으로 이미 있는 마차뜰 블록과 겹치고, 색도 컨셉보다 옅거나 어둡다(대조 시트: 이 세션 scratchpad `contact.png`). 그래서 라이브러리는 Codex로 새로 받는다.

## 4. 텍스처 파일 · 압축 · 예산

| 항목 | 규칙 |
|---|---|
| 파일 | `<이름>_Albedo.png`(sRGB, **알파 = 부드러움**), `<이름>_Normal.png`(OpenGL +Y, Unity Normal map). AO 맵 없음(손그림 방식대로 틈 그늘을 albedo에 그림). 금속은 재질 숫자 하나 |
| 원본 크기 | 1024² (rcy_textures.py 출력 `textures/library/`, git 밖 아님 — ArtSource) |
| Unity 최대 크기 | 큰 면(Stone · Flag · Shade · Marble · Tile · Oak · Walnut · Timber · Soil) 1024, 나머지 512 |
| 압축 | 자동(PC: Albedo RGBA → DXT5/BC7, Normal → BC5), 밉맵, 삼선형, 비등방 4 |
| 메모리 (PC) | 큰 면 9 × 2.67 MB + 작은 것 14 × 0.67 MB ≈ **33 MB** (밉맵 포함) |
| Mobile 품질 단계 | 같은 PC 빌드의 낮은 단계라 지금은 같음. 반으로 줄이려면 텍스처 밉맵 제한 그룹 'CarriageYard'를 Quality 설정에 넣어야 함(전역 설정이라 승규 확인 뒤) → 약 8 MB |
| 삼각형 | 그대로(UV만 더함) |
| 그리기 횟수 | 그대로 |

## 5. Unity 연결 (rcy-textures가 함)

- `textures/rcy_unity_materials.py` → `Assets/Art/PawnRushCarriageYard/Textures/<이름>_*.png` + `Assets/Resources/PawnRushCarriageYard/Materials/<이름>.mat`(URP Lit, 알파 = 부드러움, 타일링 1/타일).
- `RoyalCarriageYard.Dressing.cs`의 `Hang`: 칸 이름 `RCY | xxx` → `Materials/<모델 이름>/RCY_Xxx`가 있으면 그것, 없으면 `Materials/RCY_Xxx`, 둘 다 없으면 클레이. **다른 세션은 Dressing.cs를 고치지 않는다.** 한 모델만 다른 재질이 필요하면 `Materials/<모델>/RCY_Xxx.mat`을 요청한다.
- 말: coat · hair는 라이브러리 재질의 복사본에 털 색을 곱해 그림(갈색 #8B4A22 · 회색 #ECE8E1 · 흑 #2A2523), eye는 코드 색.
- Inspector `bakedMaterials`(끄면 전부 클레이로 비교).
- Unity가 Play 중이면(`Temp/__Backupscenes`가 있으면) Assets에 쓰지 않는다.

## 6. 각 세션 순서

1. 자기 `*_model.py`의 칸 이름을 3절 규칙대로 정리(gold → brass, 밑돌 → SHADE, 목재 → TIMBER …). 위치 · 크기 · 축 · 콜라이더 상자는 그대로.
2. `blender -b --factory-startup --python qa/rebuild_models.py -- <키>`로 다시 냄(UV는 rcy_kit이 자동). 삼각형 수가 같은지 확인.
3. `qa/texture_lookdev.py`처럼 라이브러리 재질로 렌더해 컨셉과 비교(입구 견본 방식: ACES 1.3, 노출 .6, 해 1.85 58° 남동, 하늘 보조 .22, 반사 .12).
4. 결과(구역별 삼각형 · 쓰는 칸)를 RCY_MODELS.md 자기 절에.

## 7. 받은 Codex 텍스처 목록 (11-17, 23장, 모두 1254² PNG)

받은 방법: `codex.exe exec --skip-git-repo-check --sandbox workspace-write -C textures/codex/bN -i RCY_08_props_materials.png -i U1_wagon_yard.webp -i U3_sorting_yard_cranes.webp -i RCY_07_architecture_modules.png - < bN/PROMPT.md`(세 묶음 동시, 기록 `bN/codex_log.txt`). 공통 지시: 정사각 · 이음매 없음 · 정면 평면 · 고른 조명의 albedo, 컨셉 손그림 느낌, 글자 · 문장 · 깃발 · 사람 없음, 블록 · 판자 줄눈 없음(모양이 이미 있으므로).

| 묶음 | 파일 → 라이브러리 |
|---|---|
| `codex/b1/` | rcy_cream_limestone → RCY_Stone, rcy_flagstone → RCY_Flag, rcy_shade_stone → RCY_Shade, rcy_white_marble → RCY_Marble, rcy_roof_slate → RCY_Tile, rcy_soil → RCY_Soil |
| `codex/b2/` | rcy_light_oak → RCY_Oak, rcy_walnut → RCY_Walnut, rcy_pale_timber → RCY_Timber, rcy_dark_iron → RCY_Iron, rcy_matte_brass → RCY_Brass, rcy_gold → RCY_Gold, rcy_coach_lacquer → RCY_Coach |
| `codex/b3/` | rcy_sand_canvas → RCY_Cloth, rcy_burlap → RCY_Sack, rcy_straw → RCY_Straw, rcy_rope → RCY_Rope, rcy_leather → RCY_Leather, rcy_velvet → RCY_Velvet, rcy_horse_coat → RCY_Coat, rcy_horse_mane → RCY_Hair, rcy_hoof → RCY_Hoof, rcy_plume → RCY_Plume |

처리(`rcy_textures.py`, Blender 배치): 1024²로 줄임 → Moisan 주기 분해(가장자리 차이 제거) → 가장자리 띠 16 %를 반 칸 민 사본과 섞어 무늬 이음매 숨김 → 넓은 조명 얼룩 펴기 → 평균색을 계획 색 쪽으로 70 % → albedo 세부에서 높이 → 노멀 · 부드러움(알파). 결과 `textures/library/<이름>_Albedo.png`, `_Normal.png`, `rcy_textures.json`(칸 · 타일 m · 금속 · 평균색 · 원본 경로).

다시 굽기: `blender -b --factory-startup --python rcy_textures.py -- "" [이름,이름]` → Unity: `python.exe -I textures/rcy_unity_materials.py [이름,…]`(Unity가 Play 중이 아닐 때. Play 중이면 `RCY_UNITY_ROOT=<scratch>`로 먼저 만들어 둠).
견본 렌더: `blender -b --factory-startup --python qa/texture_lookdev.py -- "" <출력 폴더> a,b,c,d`(입구 구역 + 마차 · 말 · 성문, 라이브러리 재질).

**다른 세션이 텍스처를 새로 받지 않는다.** 필요한 칸이 표에 없으면 rcy-textures에 요청 → Codex 묶음 b4로 받아 라이브러리에 넣는다.

## 8. 상태 (11-17)

- 라이브러리 23종 완료(ArtSource). 입구 견본 렌더 완료(사용자 확인 전).
- Unity 쪽(`Assets/Art/PawnRushCarriageYard`, `Resources/PawnRushCarriageYard/Materials`, `Dressing.cs`)은 **Unity가 Play 중이라 아직 안 씀** — scratchpad에 준비됨, Play가 끝나면 rcy-textures가 넣고 컴파일 확인.
- 각 구역 FBX는 각 세션이 칸 이름을 정리한 뒤 다시 낸다. 다시 내기 전까지 Unity에서 그 구역은 UV가 없어 텍스처가 한 점 색으로 보인다 → **Dressing.cs를 넣는 순간 아직 안 낸 구역이 단색처럼 보일 수 있음**. 그래서 Dressing.cs는 각 세션이 FBX를 다시 낸 뒤(또는 bakedMaterials를 끈 채) 넣는다.

## 9. 판석마다 짙은 돌 섞기 (11-17, 승규 "판석마다 짙은 돌 섞기 시작")

- **Blender:** `rcy_kit.surface_uvs`(join 끝, 자동)가 조각마다 꼭짓점 색 `Color`(FLOAT_COLOR, 선형)를 씀 — UV 오프셋과 같은 위치 해시. `TINT_SLOTS`: `RCY | flag` = 판석(약 17 % 짙은 돌 .70 ~ .84 · 8 % 따뜻하게 · 나머지 .93 ~ 1.02), `RCY | stone` · `RCY | shade` = 벽(10 % .84 ~ .92 · 나머지 .94 ~ 1.01), 그 밖의 칸은 흰색 1. `export_rcy`의 FBX가 색 층을 냄(`colors_type="LINEAR"`).
- **Unity:** `Assets/Art/PawnRushCarriageYard/Shaders/RcyTintLit.shader`(셰이더 이름 `ChessFight/RcyTintLit`) = 프로젝트의 URP Lit 그대로 + 꼭짓점 색으로 albedo 곱하기(Forward · GBuffer 패스). 생성기 `textures/rcy_tint_shader.py`(URP를 올리면 다시 돌림). `RCY_Flag` · `RCY_Stone` · `RCY_Shade` 재질이 이 셰이더를 씀(`rcy_unity_materials.py` `TINTED`).
- **주의:** 꼭짓점 색이 없는 예전 FBX는 이 세 재질에서 어둡게 보일 수 있음 → 모든 구역을 지금 rcy_kit으로 다시 냄. 입구는 11-17 다시 냄.
- **비용:** 정점 색 4바이트 · 보간값 하나 추가, 텍스처 · 재질 · 그리기 횟수 그대로.
