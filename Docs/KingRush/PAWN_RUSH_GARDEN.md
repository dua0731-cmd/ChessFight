# 폰 러쉬 왕실 수로 정원 — 시험 씬 구역 G (R263)

> **R309 Unity v33 — 남은 블록아웃 덮기:** `build_v33.py`가 v31 블록아웃 위에 모델을 더 얹음(묶음 `V33 | v31 layout dressing`, 이름 `V19 | V33 …`). 바닥 · 벽 · 둑 · 터널 · 계단 · 출구 = V19 벽돌 모듈 타일, 난간 = V21 기둥 + 갓돌, 섬 · 기둥 줄기 = 둥근 벽돌 기둥, 지름길 K1~K3 = 1.2 m 통 발판 · K4 = 2.6 m 통 + 회전봉. 새 회색 클레이: 배 8척 · 수문 판 3 · 연잎 2 · 회전봉 6 · 간헐천 테두리 8. 움직이는 것은 메시 역할의 `attach`로 코드 부품(`Boat L-N`, `Sluice gate n`, `Shortcut post Kn`, `Shortcut lotus Ln`, `Spin bar n/Arm k`, `Bobbing platform Pn`)에 붙음. V33 부품은 충돌체 없음. Version 34.

> **R307 Unity v32 — main v31 배치:** 구역 G는 main의 `Early_1-1_RoyalWaterGarden` 프리팹(v31, `RoyalWaterGardenBuilder`)을 짓고 그 위에 정원 모델을 얹음. 모델 좌표 = v31 구간 좌표(Blender x, y, z → 구간 x, z, y). 옮긴 모델: 성문 묶음 · 승선 블록(높이 5.0 → 3.8), 통 발판 → P1~P8 위(`attach`, 코드 발판의 자식), 로봇 팔 2대 → 둑 남쪽 x 22.5 · 10.5. 모델이 덮는 블록아웃은 렌더러만 끄고 충돌은 그대로. 지운 모델 · 알려진 겹침은 HANDOFF R307과 `ArtSource/PawnRush/Garden/v32_report.json`. 미확인.

> **R296 Unity v31:** v30과 같은 물체 · 이름 · 위치 · 충돌 역할, 삼각형만 646만 → 399만. V19 공유 메시 3개(블록 92개)가 21,404 → 2,600(35° 넘는 모서리 각지게), V16 지그재그 발판 6개 · 왕실 배 메시(배 2척)가 약 9.3만. 만든 곳 `build_v31.py` → `export_unity_v31.py` → `check_unity_export_v31.py`, Version 27.

> **R287 Unity v30:** 같은 이름 FBX/JSON(313메시). 출발 대기장 바닥 = `V19 | 출발_12명대기_12x9m` 한 장(옛 `… 0.0 / 0.1 / 1.0 / 1.1` 네 장 대신, box 충돌). 둥근 섬(`V02_장식정원_독립섬` · `V19 | V02_장식섬_벽` · 화단) 없음, 양옆 테라스 · 수조 · 폭포 · 나무는 y +10.5. 새 `V30 | castle water gate`(box) · `V30 | gate channel water`(물) · `V30 | parade boat PIVOT` 아래 `V30 | parade royal boat`(충돌 없음). 매니페스트 `parade`(track · fps 30 · frames 2111 · samples 7개씩 · halfLength 3.8 · halfWidth 1.94 · forward = 배 자기 +x) → `GardenBoatParade`(Time.time 재생, 출발 대기장 |x|<7 · −61<z<−50에 들어오면 2초 뒤, 광장 |x|<19 · z −16을 북쪽으로 넘으면, 최대 2번, 쉴 때 비활성). `GardenWaterInteraction` 배 3척 · 배 자기 기준 앞 방향 · 비활성 배 무시, 셰이더 전역 `_GardenHull*[3]`.

> **R286 Unity v29:** 같은 이름 FBX/JSON(391메시). 둘레 = `V29 | castle wall {변}{번호} {구간}`(구간 메시 공유, 충돌 없음) · `V29 | castle corner tower {SW/NW/NE/SE}` · 숨은 충돌 판 `V29 | castle collision {W/E/N/S}`(proxy: MeshCollider · 렌더러 끔) · `V29 | outer meadow`. 옛 `V17 | castle wall` · `V19 | 성벽_*` · `외벽_흉벽` · `V04_성벽_탑지붕` 없음. 원본 모델 `ArtSource/PawnRush/Garden/Castle/*.glb`.

> **R285 Unity v28:** 같은 이름 FBX/JSON(527메시). `V04_중반_분수` 없음, `V23 | garden/planter bushes · flowers`는 건널목 · 둥근 단 덤불을 빼고 바닥에 내림. 배 PIVOT 트랙은 키 프레임(벽 앞 y 38.96 · 45.52에서 쿵 튕김), 런타임 코드는 그대로 샘플을 재생.

> **R284 Unity v27:** 같은 이름 FBX/JSON. `V26 | spout shadow …` 면이 모두 벽 밖을 봄(뒷면이 잘려 막혀 보이던 4곳), 수조 물줄기는 난간벽 구멍 안에서 시작. 물 재질 = 색 E, 수조 물(`V22 | tank water`)은 `PR_GardenWaterShallow`(같은 셰이더, 물결 0.006m — 수조가 3cm 깊이). 물 셰이더 새 속성 `_BoatRingHeight` · `_BoatGlow`, `Interaction`이 선체에 쌓인 물 정도(`hull`)도 돌려줌. 배 드라이버 9cm · 0.06rad. `Version=19`. 커밋 안 함, Unity 확인 전.

> **R283 Unity v26:** 같은 이름 `RoyalWaterGarden.fbx`/`_Motion.json`(528메시). 분출구 = `V26 | spout stone …`(테두리 · 홈통 · 수조 난간벽, 상자 충돌체) + `V26 | spout shadow …`(어두운 입구, 색 유지 — `Coloured`에 `shadow`), 폭포 = `V26 | fall curtain/streams …`(소스 이름으로 `PR_GardenFallSheet`), 착수선 17개(`water_interaction_v26.json`). 물 셰이더: `_BodyDepth` · `_CrestColor` · `_ChurnColor` · `_ChurnReach`, 착수선 배열 `_GardenLanding*`(24), 배 `_GardenBoatInfo`(들썩임 m/s · 흔들림 rad/s). 폭포 셰이더: 큐 Transparent+5, `_GardenFoot`(해자 수면 높이) · `_PoolTint` · `_FootHeight`. 보트 드라이버 7cm · 0.03rad · 0.05rad. `Version=18`. Unity 확인 전.

> **R281 Unity v25:** `RoyalWaterGarden_v25.fbx`/`_Motion.json`. 폭포 = `V25 | fall curtain …` + `V25 | fall streams …`(정점 색: r 가로, g 진행도, b 시드, a 물줄기=1), `GardenWaterLook.ApplyBySource`가 소스 이름으로 `PR_GardenFallSheet` 연결. 저수지 수면 +0.24m(약 3.5m), 보트 피벗 드라이버로 위아래·앞뒤·좌우 흔들림. 물 셰이더는 흰 거품 없이 굴곡만: `BoatWaves`(켈빈 항적 · 선수/선미파 · 고리), `RingSlope`(폭포 · 발판). `_WakeHeight`. `Version=16`. Unity 확인 전.

> **R280 Unity v24:** `RoyalWaterGarden_v24.fbx`/`_Motion.json`(트랙13: 보트2 · 진자2 · 팔꿈치2 · 회전봉 · 지그재그 발판6, `bobbers` 발판 트랙 · 반지름3m). C발판 구역 24→28m: 입구 턱 y -13~-10 · 높이 1.95m, 경사로 y -20~-13, 발판 중심 (∓2.9, -7+4.4k), 윗면 2.05+0.5k(+0~0.7 흔들림, 위상 -60°k). 물 셰이더 Gerstner(`_WaveHeight/_WaveLength/_WaveHeading/_WaterDepth/_Choppiness`), 발판 고리 `_RippleHeight`. `Version=15`. Unity 확인 전.

> **R279 Unity v23:** `RoyalWaterGarden_v23.fbx`/`_Motion.json`(+`splashes` 폭포 착수선 9개, `boats` 선체 반길이·반폭·진행축). `GardenWaterInteraction`(빌더가 붙임)이 매 프레임 `_GardenBoat*`/`_GardenSplash*` 셰이더 전역 배열을 채워 `PR_GardenClearWater`가 항적·거품·고리를 그림(읽기만, 이동 없음). 재질 매핑: `V23 | waterfall sheet` → `PR_GardenFallSheet`, `V23 | fall spray` → `PR_GardenSpray`, 잎·꽃 이름은 거칠기 .92. 클레이 제외 단어: tree/water/bush/flower/fall/spray. 물은 `ZWrite On`(폭포 발이 물 밑으로 비치지 않게). `Version=14`. Unity 확인 전.

> **R278 Unity v22:** `RoyalWaterGarden_v22.fbx`/`_Motion.json`. 로봇팔 1.25배·IK 6초 루프(감기1초 → 0.85초 스윙, 길 바닥 위 0.25m → 반대편 0.75초 정지 → 1.8초 복귀 → 정지, 2번 팔 3초 늦게), `V22 | robot k elbow knuckle`(팔꿈치 자식)·`shoulder hinge`(고정). 테라스 수조 폭포를 수조 높이로, `V22 | tank water`(물 재질), `V22 | reservoir … spout`(박스 충돌). 물 재질 `PR_GardenWater`는 새 셰이더 `PR_GardenClearWater`(URP 깊이·불투명 텍스처 필요), 폭포 셰이더도 굴절형. 시작점은 가장 가까운 바닥(`TryFloor` 정렬). `Version=13`. Unity 확인 전.

> **R277 Unity v21:** `RoyalGarden`은 `RoyalWaterGarden_v21.fbx`/`_Motion.json`을 읽는다(v20 Resources 파일 삭제). 회전봉에 섞였던 경계석·허브 고리 조각을 고정 오브젝트로 분리(벽 뜯김 원인, [PITFALLS §25](../Environment/PITFALLS.md)), 남북 입구 절단면 제거 후 석조 기둥벽·갓돌·기둥(박스 충돌), 허브 고정 고리, 나무 12그루 → 사용자 GLB 2종(`ArtSource/PawnRush/Garden/source_glb/`, 색 유지·충돌 없음). 충돌 역할 `proxy`: 숨긴 높이장 `V21 | plaza collision proxy`가 메시 충돌체, 장식 광장과 보수 포장은 충돌 없음. `SeoungTestCourse.Version=12`. Unity 확인 전.

> **R276 Unity v20:** 이제 `RoyalGarden`은 v06 대신 `Resources/PawnRushGarden/RoyalWaterGarden_v20.fbx`와 `_Motion.json`을 읽는다. 보이는 메시418/동작15트랙, 24초 반복을 공유 시계로 물리 스텝마다 보간. 이동 루트 Kinematic/`IMovingSurface`, 경사/아치/동적 메시 충돌체와 정적 모듈 상자 충돌체, 새 물 슬롯·단색 URP 재질, 저수지 낙사 높이 갱신. 시험 씬 버전11: **ChessFight > SeoungTest > Build Pawn Rush Test Scene → Play**. Blender 조명/컴포지터/멀리 있는 배경은 시험 씬에 안 넣음. Core133 및 FBX/트랙 재가져오기 점검 통과, Unity 편집기 미연결로 Unity 컴파일/화면/승선/가동간섭/충돌/성능/사용자 확인 전. 아래 v06 설명은 이전 기록이며 최신은 `ArtSource/PawnRush/Garden/UNITY.md`.

> **R275 애니메이션:** v20 Blender에서 기물3개/후반 촛대 제거, 회전봉 시계방향8초·C발판 상하6초(0~0.7m)·어깨/팔꿈치 로봇팔6초·양팀 보트 반대위상 왕복12초(별도 x±6 통로에서 y38~45). 광장바닥/화단/팔 지지부 고정. 30fps 1~720프레임, 설명 `ArtSource/PawnRush/Garden/ANIMATION.md`. Unity/충돌/실제 승선/가동간섭/사용자 확인 전.

> **R274 나머지 구조물:** v19 Blender에 벽 GLB를 원형 성탑/기둥, 낮은 바닥/기단/테라스/승선장, 옹벽/경사로로 변형. 기존174개와 작은 기둥10개 교체 → 공유 메시 구조241개. 경사로 주 상단0→3m 및 기존 구조 범위 기준. 텍스처 없음, 원본 숨김 보존, C발판8/진자피벗2 유지 점검. 교체 목록 `structure_report_v19.json`. Unity/바닥 평탄성/충돌/동작/성능/사용자 확인 전.

> **R273 광장/진자 교체:** v18 Blender의 새 원형 광장은 외곽36m·주 바닥0m로 맞춤, 남북 입구와 연결 바닥 보완. 에셋에 회전봉/화단이 포함되어 원래 광장 요소는 숨김 보존. 보트 전 진자2개는 새 철구 에셋의 지지부/가동부를 분리, 가동부만 기존 피벗에 연결. 단색 클레이/텍스처 없음. 광장 회전봉은 통합 정적 메시이며 Blender 드라이버 미복원. Unity/실제 통과폭/충돌/애니메이션/사용자 확인 전.

> **R272 성문/성벽 배치 정정:** v17 Blender에 아치를 끝 지점(y55, 바닥5.042m)으로 옮겨 기존 성문 외곽 폭36m·두께4m·상단19.91m에 맞춤. 정사각형 벽을 길이6m·두께2m·높이6m로 변형해 기존 외곽벽 x±47에 20개씩 연결. 끝의 성문-탑 사이도 연결벽2개로 이음. 단색 재질, 기존 모델 숨김 보존. 렌더/바운딩 점검만; 실제 통과폭/충돌/Unity/사용자 확인 전.

> **R271 사용자 에셋:** v16 Blender에 GLB4종을 텍스처 없이 배치(아치1 · C발판8 · 팀 보트2 · 벽 끝 석조4). 클레이 재질1개/이미지 노드 없음. 기존 모델은 숨겨 보존, 새 보트는 팀 피벗에 연결. 렌더와 파일 재열기 점검만, Unity/승선 높이/충돌/동작/사용자 확인 전.

> **R270 광장 조명/색감:** v15 Blender에서 햇빛/하늘빛, 하늘 파랑, 채도와 대비를 조정. 같은 플레이 높이 카메라에서 v14와 비교, 원본 보존. 색 보정은 Blender5.2 컴포지터 그룹이며 Unity 포스트프로세싱으로 이전하지 않음. 렌더 확인만, 사용자/Unity 확인 전.

> **R269 광장 원경:** v14 Blender의 맵 바깥에 산2겹/구름4무리와 카메라 하늘색 변화를 추가. 성문 실루엣을 유지하도록 낮고 완만한 능선, 기존 조명/카메라/동선 유지. 카메라 배경용 하늘은 Unity 셰이더가 아님. Unity 이전/성능/사용자 확인 전.

> **R268 광장 석조:** v13 Blender에서 낮은 벽20개 · 화단 받침4개를 엇갈린 석재, 줄눈, 베벨, 갓돌로 다듬음. 기존 벽은 Blender에서 숨겨 보존. 작전 회의실 계열의 따뜻한 중성색 유지. 전체/확대 렌더로 비교, Unity 반영/사용자 확인 전.

> **R267 광장 화단:** `RoyalWaterGarden_v12.blend`에 입체 꽃잎/꽃술/줄기/잎과 다섯 가지 꽃 색 추가. v11 카메라와 조명으로 비교. Blender 아트 작업이며 Unity 모델/코드는 그대로, Unity 이전 및 성능 확인 전.

> **R266 광장 아트 기준 1차:** `ArtSource/PawnRush/Garden/RoyalWaterGarden_v11.blend`에 방사형 포장, 모서리/색 변화, 금속 거칠기, 햇빛과 하늘빛, 높이2.5m 카메라 추가. `plaza_player_v11.png`로 비교. Blender 렌더만 확인했으며 참고 이미지 수준까지 식생/벽/배경 디테일, Unity 반영은 후속. 기존 v10 원본 보존.

> **R265 Unity 물 연결(2026-11-01):** 정원 생성 직후 `GardenWaterLook.Apply`가 G09 수면을 `Resources/PR_GardenWater`, G10 폭포를 `Resources/PR_GardenWaterfall` 재질로 교체한다. 수면은 기존 `ChessFight/Pawn Rush Water` 셰이더(교차 이동 노멀 · 프레넬 하늘 반사 · 햇빛 반짝임), 폭포는 새 `ChessFight/Garden Waterfall` 셰이더(월드 좌표 · 세로 흐름)를 쓴다. 원본 폭포/꽃 합친 메시에는 UV가 없어 기존 UV용 폭포 셰이더를 직접 쓰면 안 된다. 슬롯만 교체해 꽃 · 석재 · 기존 거품/충돌을 유지한다. `SeoungTestCourse.Version = 10`: Play 시작 시 다시 조립하거나 **ChessFight > SeoungTest > Build Pawn Rush Test Scene**. FBX 배치와 낙사 범위는 기존 v06, Blender v10의 고저차/벽은 Unity에 옮기지 않음. Core 133개 통과(.NET 10); Unity 편집기 미연결로 Unity C#/셰이더 컴파일 · 화면 · 사람 확인 전.

> **R264 Blender 작업본(2026-11-01):** `ArtSource/PawnRush/Garden/RoyalWaterGarden_v10.blend`는 v06 FBX를 가져와 경사로와 C자 발판의 고저차, 작전 회의실 벽 패널, 잔물결 · 폭포 애니메이션과 사실적인 물 재질을 편집한 별도 작업본이다. 30fps, 1~120프레임. **Unity FBX와 시험 씬은 여전히 아래 v06이다.** 원래 `.blend`가 아니라 FBX에서 복원했으므로 회전봉 · 진자 · 보트의 Blender 드라이버는 없다. Blender 물 재질/애니메이션을 Unity에 옮기려면 별도 셰이더와 동작 구현, 높아진 물의 낙사 범위 · 이동 피벗 · 충돌 재점검이 필요하다. Blender 렌더 · 데이터 점검만, Unity/사용자 확인 전.

2026-11-01, 승규 님. 클라우드 채팅에서 Blender로 만든 정원 맵을 `seoungtest`의 폰 러쉬 시험 씬(`SeoungTest_PawnRush`)에 **도서관 컨베이어 코스(구역 E) 옆 구역 G**로 넣었다. Blender 배치 그대로의 시험용이며 최종 아트 · 밸런스가 아니다. `seoungtest`에 커밋 · 푸시, **Unity · 사람 확인 전.**

## 1. 원본과 버전

v06 당시 맵 원본(`.blend` · 스크립트 · 렌더)은 저장소 밖(승규 님 PC의 Git 제외 폴더, 클라우드 채팅 ZIP `ChessFight_Garden_v06_Claude.zip`)에 있었고 저장소에는 Unity FBX만 넣었다. 이후 v06 FBX에서 복원해 편집한 v10 Blender 작업본은 위 R264 경로에 있다.

| 버전 | 한 것 |
|---|---|
| v03 | 승규 님이 정한 그레이박스: 회전봉 광장 → 상승 지형 → 백팀 왼쪽 C자 발판 4개 · 흑팀 오른쪽 반대 C자 4개 → 진자길 합류 → 상단 승선장 · 팀 보트 → 성문 |
| v04 | 컨셉 그림대로 실제 형태(사암 벽 · 화단 · 청동 회전봉 · 청록 · 금 드럼 발판 · 파랑 · 금 해머 · 곤돌라 보트 · 폭포 · 분수 · 연잎). 판정 치수는 v03 그대로, 새 물체는 모두 장식(`role=decor`, `collision=none`, 이름 `V04_*`) |
| v05 | 벽 · 석조를 도서관 컨베이어 코스 FBX의 재질 `CLAY \| uniform neutral gray`(0.54 회색, 거칠기 0.78, 금속 0)로, **텍스처 없음**(모든 재질 단색). 내보낸 FBX의 클레이 재질 속성 13개가 도서관 FBX와 같음 |
| v06 | 촛대 모양(원뿔) 나무 32그루 지움(장식정원 · 테라스 사이프러스 28, 광장 화단 원뿔 4). 둥근 나무 12그루는 남김 |

## 2. 배치(존 좌표, m)

FBX는 Blender (x, y, z)를 Unity (−x, z, −y)로 들여온다([PITFALLS §24](../Environment/PITFALLS.md)). 도서관처럼 180° 돌린 피벗 아래에 두어 존 좌표 = **(Blender x, Blender z, Blender y)**: 오른쪽 x, 위 y, 진행 방향(출발 → 성문) +z. 크기 약 96 × 120 m.

| 곳 | z | 바닥 높이 y |
|---|---|---|
| 출발 대기(12 × 9) · 시험 씬 출발점 (0, 0, −57) | −60 ~ −51 | 0 |
| 회전봉 광장(지름 34, 가운데 z −35) | −52 ~ −18 | 0 |
| 광장 출구 경사 지형(폭 12) | −20 ~ −9 | 0 → 1.8 |
| 하부 단층(팀별 진입, 폭 26) | −9 ~ −6 | 1.8 |
| C자 발판(반지름 3.05): 백팀 x −5 · −9.6 · −9.6 · −5, 흑팀 대칭 | −3 · 2.5 · 9.5 · 15 | 2.24 · 2.64 · 3.04 · 3.44 |
| 상부 단층(합류, 폭 24) | 18 ~ 21 | 3.8 |
| 진자길(폭 8, 진자 z 22 · 27) | 21 ~ 30 | 3.8 |
| 공용 승선 대기장(20 × 7) · 팀 부두(x ±6) | 28 ~ 35 | 3.83 |
| 팀 보트(x ±6) | 38 ↔ 45 | 갑판 3.83, 물 2.1 |
| 도착 성문 앞마당(28 × 10.5) | 49.5 ~ 60 | 3.84 |

## 3. Unity 연결

| 무엇 | 어디 |
|---|---|
| 모델 | `Assets/Resources/PawnRushGarden/PawnRushRoyalWaterGarden.fbx`(Git LFS, 약 5 MB, 메시 419 · 빈 물체 5 · 재질 26) |
| 가져오기 설정(`.meta`) | 도서관 환경 FBX 설정을 바탕으로: 재질 내장(`materialImportMode: 1`, 도서관 벽과 같은 방식), 애니메이션 없음. **배율 1 · 단위 변환 켬**(이 FBX는 미터 단위 `UnitScaleFactor 100`; 도서관 FBX는 센티미터라 배율 0.01 · 단위 변환 끔), 메시 읽기 켬(Play 때 지어도 MeshCollider가 붙게) |
| 짓는 코드 | `Course01v4Builder.RoyalGarden.cs`(`RoyalGarden(b)`), 구역 목록 `Course01v4Builder.Zones.cs`(`E`, `G`), `SeoungTestCourse.Version` 8 → 9 |
| 자리 | 시험 씬이 구역을 x 방향 한 줄에 80 m 간격으로 세우므로 도서관(E)의 동쪽 80 m 옆. 남쪽 끝이 z 0 |
| 충돌체 | 모델의 메시마다 `MeshCollider`(그 메시 그대로). 장식(`V04_*`)과 물 두 장(`해자_시각영역_물판`, `V03_상단_수면_높이2.1m`)은 없음 |
| 물에 빠짐 | `KillVolume` 둘(1.5초 뒤 구역 출발점): 해자 전체(y −3 ~ −1.45), 위 저수지(x ±18, z 31 ~ 59, y 1 ~ 1.95) |

### 3.1 움직이는 것 — Blender 드라이버 → Unity

모두 `ObstacleClock` 시간의 순수 함수(절대 규칙 7), 기존 `Obstacle` 컴포넌트를 피벗에 붙이고 자식 메시가 따라 움직인다. 시계 0초 = Blender 1프레임. 위 → 아래로 볼 때 같은 방향으로 돌도록 각도 부호를 바꿨다(존 좌표가 Blender 좌표의 거울이라).

| 장애물 | Blender | Unity |
|---|---|---|
| 회전봉(팔 4개) `회전봉_PIVOT_Z_8초1회전` | Z축 (frame−1)·2π/240, 8초 한 바퀴 | `Spinner` 존 위쪽 축 −45°/초 |
| 진자 둘 `V02_필수진자_PIVOT`(.001) | Y축 0.65 rad·sin(2πt/8 + 0 · π) | `Pendulum` 존 진행 축, 진폭 −37.24°, 8초, 두 번째 위상 4초 |
| 팀 보트 둘 `V02_백팀_보트_PIVOT` · `V02_흑팀_보트_PIVOT` | y = 41.5 − 3.5 cos(2πt/12) | `Oscillator` 진행 축 ±3.5 m, 12초, 위상 9초(피벗을 가운데 41.5로 옮겨 놓음). 갑판에 선 폰은 `IMovingSurface`로 같이 감 |

## 4. 알고 있는 것 · 아직 안 한 것

- **진자 해머가 지지 기둥을 지나감:** 가장 크게 흔들릴 때(2 · 6초) 해머(x ±2.35 ~ 6.25)가 기둥(x ±4.38 ~ 5.23)과 겹친다. v03 배치부터 있던 것(그때 공 지름 3 m도 같음). Unity에서는 움직이는 쪽이 Kinematic이라 부딪히지 않고 겹쳐 보이기만 한다. 기둥을 벌리거나 진폭을 줄일지 결정 대기.
- **회전봉 높이:** 팔이 바닥에서 약 0.7 ~ 1.4 m(띠 포함)라 지금 폰 러쉬 점프로 넘기 어렵고 숙여 지나갈 수도 없다(v03 그대로). 의도인지 Play로 확인.
- 팀 전용 승선(보트의 `team_only`), 체크포인트, 결승 · 골 판정, 미션 광장, 난간, `NoClimbSurface`는 아직 없다. 떨어지면 구역 출발점으로 간다.
- 장식에는 충돌체가 없다(꽃 · 나무 · 폭포 · 분수 · 갓돌 · 진자 받침 기둥 · 회전 고리 포함).
- 맵을 Blender에서 다시 고치면 같은 이름으로 FBX를 바꿔 넣고 `SeoungTestCourse.Version`을 올린다. 피벗 · 물 이름이 바뀌면 `RoyalGarden`의 이름 목록도 고친다.
